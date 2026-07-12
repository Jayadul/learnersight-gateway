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

    // ── Load authorization.json into config ────────────────────────────────
    builder.Configuration.AddJsonFile("authorization.json", optional: false, reloadOnChange: true);

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
