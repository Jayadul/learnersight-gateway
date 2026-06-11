using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using NetZeroGateway.API.Extensions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Infrastructure.Persistence;
using Serilog;

// ── Configure Serilog early so startup errors are captured ────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog from appsettings ───────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, loggerConfig) =>
        loggerConfig.ReadFrom.Configuration(ctx.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithThreadId()
                    .Enrich.WithCorrelationId());

    // ── Services ───────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("gateway", new OpenApiInfo
        {
            Title = "NetZeroGateway — Gateway API",
            Version = "v1",
            Description = "REVE SMS-compatible endpoint. x-soft calls POST /sendtext — zero config changes needed."
        });
        c.SwaggerDoc("admin", new OpenApiInfo
        {
            Title = "NetZeroGateway — Admin API",
            Version = "v1",
            Description = "Admin panel: channel configs, logs, dashboard, users, fallback policies."
        });

        c.DocInclusionPredicate((docName, apiDesc) =>
        {
            var groupName = apiDesc.ActionDescriptor.EndpointMetadata
                .OfType<Microsoft.AspNetCore.Mvc.ApiExplorerSettingsAttribute>()
                .FirstOrDefault()?.GroupName;

            // If no group set, include in admin doc by default
            return groupName == docName || (groupName is null && docName == "admin");
        });

        var jwtScheme = new OpenApiSecurityScheme
        {
            BearerFormat = "JWT",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            Reference = new OpenApiReference { Id = "Bearer", Type = ReferenceType.SecurityScheme }
        };
        c.AddSecurityDefinition("Bearer", jwtScheme);
        c.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, [] } });
    });

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplicationServices();
    builder.Services.AddJwtAuth(builder.Configuration);
    builder.Services.AddCorsPolicy(builder.Configuration);

    var app = builder.Build();

    // ── Migrate + seed ─────────────────────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<NetZeroGatewayDbContext>();
        await db.Database.MigrateAsync();          // needs using Microsoft.EntityFrameworkCore
        await DataSeeder.SeedAsync(scope.ServiceProvider);

        // ── Load x-soft base URL from DB and register as allowed CORS origin ──
        var xSoftRepo = scope.ServiceProvider.GetRequiredService<IXSoftAppConfigRepository>();
        var xSoftCfg = await xSoftRepo.GetActiveAsync();
        if (xSoftCfg is not null && !string.IsNullOrEmpty(xSoftCfg.BaseUrl))
        {
            Log.Information("CORS: allowing x-soft origin from DB: {Url}", xSoftCfg.BaseUrl);
            app.UseCors(policy => policy
                .WithOrigins(xSoftCfg.BaseUrl)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());
        }
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
