using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NetZeroGateway.API.Extensions;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.Persistence;
using Serilog;

// ── Configure Serilog early so startup errors are captured ────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // A GET /sendtext carrying several hundred recipients is a 15 KB+ request line; Kestrel's
    // 8 KB default answered it with 414 before the request reached any controller.
    builder.WebHost.ConfigureKestrel(o =>
    {
        o.Limits.MaxRequestLineSize = 128 * 1024;
        o.Limits.MaxRequestHeadersTotalSize = 256 * 1024;
    });

    // ── Load authorization.json into config ────────────────────────────────
    builder.Configuration.AddJsonFile("authorization.json", optional: false, reloadOnChange: true);

    // CreateBuilder has already registered the environment-variable and command-line
    // sources, and in .NET configuration the last source added wins — so the file above
    // was silently outranking both. That is why Providers__Voice__BaseUrl could be set in
    // the container, confirmed with printenv, and still be ignored at dispatch time.
    // Re-adding them puts the deployment back on top of the file, where it belongs.
    builder.Configuration.AddEnvironmentVariables();
    builder.Configuration.AddCommandLine(args);

    // ── Serilog from appsettings ───────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, loggerConfig) =>
        loggerConfig.ReadFrom.Configuration(ctx.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithThreadId()
                    .Enrich.WithCorrelationId());

    // ── Services ───────────────────────────────────────────────────────────
    builder.Services.AddControllersWithAuthPolicy(builder.Configuration);
    builder.Services.AddEndpointsApiExplorer();

    var cfg = builder.Configuration;
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("gateway", new OpenApiInfo
        {
            Title = cfg[ConfigKeys.SwaggerGatewayTitle] ?? "Gateway API",
            Version = "v1",
            Description = cfg[ConfigKeys.SwaggerGatewayDesc]
        });
        c.SwaggerDoc("admin", new OpenApiInfo
        {
            Title = cfg[ConfigKeys.SwaggerAdminTitle] ?? "Admin API",
            Version = "v1",
            Description = cfg[ConfigKeys.SwaggerAdminDesc]
        });

        c.DocInclusionPredicate((docName, apiDesc) =>
        {
            var groupName = apiDesc.GroupName;
            return groupName == docName || (groupName is null && docName == "admin");
        });

        var schemeId = cfg[ConfigKeys.SwaggerSecSchemeId] ?? "Bearer";
        var jwtScheme = new OpenApiSecurityScheme
        {
            BearerFormat = "JWT",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Reference = new OpenApiReference { Id = schemeId, Type = ReferenceType.SecurityScheme }
        };
        c.AddSecurityDefinition(schemeId, jwtScheme);
        c.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, [] } });
    });

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplicationServices();
    builder.Services.AddBackgroundJobs(builder.Configuration);
    builder.Services.AddJwtAuth(builder.Configuration);
    builder.Services.AddCorsPolicy(builder.Configuration);

    var app = builder.Build();

    // ── Migrate + seed ─────────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<NetZeroGatewayDbContext>();
        await db.Database.MigrateAsync();
        await DataSeeder.SeedAsync(scope.ServiceProvider);

        // ── Recurring jobs — always scheduled in the business time zone ────
        // Expired media cleanup: every Friday at 00:00 Dhaka time.
        scope.ServiceProvider.GetRequiredService<IRecurringJobManager>().AddOrUpdate<IMediaLibraryService>(
            "media-cleanup",
            s => s.CleanupExpiredAsync(),
            Cron.Weekly(DayOfWeek.Friday, 0, 0),
            new RecurringJobOptions { TimeZone = AppTime.Resolve(app.Configuration) });

        // Delivery status: pulled every hour (hotfix batch 9, Task 2 — was once a day at
        // 00:00 Dhaka). A daily pass left a message's status unresolved for up to 24
        // hours after it actually landed with the provider; the scope stays "everything
        // still waiting for a status" (hotfix batch 7's simplification), so this is only
        // a frequency change, not new date-window logic to maintain.
        scope.ServiceProvider.GetRequiredService<IRecurringJobManager>().AddOrUpdate<IStatusSyncService>(
            "delivery-status-sync",
            s => s.SyncAllAsync(),
            Cron.Hourly(),
            new RecurringJobOptions { TimeZone = AppTime.Resolve(app.Configuration) });

        // Provider balances: refreshed on the quarter-hour so the dashboard's existing
        // 15-minute cycle always has a recent figure to show, rather than introducing a
        // second interval for an operator to reason about.
        scope.ServiceProvider.GetRequiredService<IRecurringJobManager>().AddOrUpdate<IChannelBalanceService>(
            "channel-balance-refresh",
            s => s.RefreshAllAsync(),
            "*/15 * * * *",
            new RecurringJobOptions { TimeZone = AppTime.Resolve(app.Configuration) });
    }

    // ── Swagger ────────────────────────────────────────────────────────────
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/gateway/swagger.json", "Gateway API");
            c.SwaggerEndpoint("/swagger/admin/swagger.json", "Admin API");
            c.RoutePrefix = "swagger";
            c.DocumentTitle = "NetZeroGateway API";
        });
    }

    // ── Pipeline ───────────────────────────────────────────────────────────
    app.UseNetZeroGateway();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
