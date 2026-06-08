using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Infrastructure.HttpClients;
using NetZeroGateway.Infrastructure.Persistence;
using NetZeroGateway.Infrastructure.Repositories;
using NetZeroGateway.Infrastructure.Security;
using NetZeroGateway.Infrastructure.Strategies;
using System.Text;

namespace NetZeroGateway.API.Extensions;

public static class ServiceCollectionExtensions
{
    // ── Infrastructure ────────────────────────────────────────────────────
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // EF Core (PostgreSQL + snake_case naming)
        services.AddDbContext<NetZeroGatewayDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("DefaultConnection"))
                .UseSnakeCaseNamingConvention());

        // Repositories (Application.Interfaces → Infrastructure implementations)
        services.AddScoped<IAdminUserRepository,       AdminUserRepository>();
        services.AddScoped<IXSoftApiKeyRepository,     XSoftApiKeyRepository>();
        services.AddScoped<IXSoftAppConfigRepository,  XSoftAppConfigRepository>();
        services.AddScoped<IChannelConfigRepository,   ChannelConfigRepository>();
        services.AddScoped<IMessageRequestRepository,  MessageRequestRepository>();
        services.AddScoped<IDispatchJobRepository,     DispatchJobRepository>();
        services.AddScoped<IApiLogRepository,          ApiLogRepository>();
        services.AddScoped<IFallbackPolicyRepository,  FallbackPolicyRepository>();

        // Encryption
        services.AddScoped<IEncryptionService, AesEncryptionService>();

        // Voice report gateway abstraction
        services.AddScoped<IVoiceReportGateway, VoiceReportGateway>();

        // ── HTTP Clients (centralized — base URLs from config, Polly from config) ──
        var retryCount    = config.GetValue<int>("HttpClients:Retry:MaxAttempts",    3);
        var retryDelay    = config.GetValue<int>("HttpClients:Retry:DelaySeconds",   2);
        var timeoutSec    = config.GetValue<int>("HttpClients:TimeoutSeconds",       30);

        services
            .AddHttpClient<IReveSmsClient, ReveSmsClient>(client =>
            {
                client.BaseAddress = new Uri(
                    config["Providers:Sms:BaseUrl"] ?? "http://smpp.revesms.com:7788");
                client.Timeout = TimeSpan.FromSeconds(timeoutSec);
            })
            .AddStandardResilienceHandler(o => ConfigureResilience(o, retryCount, retryDelay));

        services
            .AddHttpClient<IWaTrendClient, WaTrendClient>(client =>
            {
                client.BaseAddress = new Uri(
                    config["Providers:WhatsApp:BaseUrl"] ?? "https://app.watrend.com");
                client.Timeout = TimeSpan.FromSeconds(timeoutSec);
            })
            .AddStandardResilienceHandler(o => ConfigureResilience(o, retryCount, retryDelay));

        services
            .AddHttpClient<INetZeroVoiceClient, NetZeroVoiceClient>(client =>
            {
                client.BaseAddress = new Uri(
                    config["Providers:Voice:BaseUrl"] ?? "http://api.sms.netzero.com.bd:8000");
                client.Timeout = TimeSpan.FromSeconds(timeoutSec);
            })
            .AddStandardResilienceHandler(o => ConfigureResilience(o, retryCount, retryDelay));

        services
            .AddHttpClient<INetZeroReportClient, NetZeroReportClient>(client =>
            {
                client.BaseAddress = new Uri(
                    config["Providers:VoiceReport:BaseUrl"] ?? "http://api.sms.netzero.com.bd:8000");
                client.Timeout = TimeSpan.FromSeconds(
                    config.GetValue<int>("HttpClients:ReportTimeoutSeconds", 60));
            })
            .AddStandardResilienceHandler(o => ConfigureResilience(o, retryCount, retryDelay));

        // ── Channel sender strategies (Factory pattern) ───────────────────
        services.AddScoped<IChannelSender, SmsSender>();
        services.AddScoped<IChannelSender, WhatsAppTextSender>();
        services.AddScoped<IChannelSender, WhatsAppMediaSender>();
        services.AddScoped<IChannelSender, VoiceSender>();
        services.AddScoped<IChannelSenderFactory, ChannelSenderFactory>();

        return services;
    }

    // ── Application Services ──────────────────────────────────────────────
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services)
    {
        services.AddScoped<IEncryptionService,             AesEncryptionService>();
        services.AddScoped<ITemplateParserService,         TemplateParserService>();
        services.AddScoped<IXSoftApiKeyService,            XSoftApiKeyService>();
        services.AddScoped<IAuthService,                   AuthService>();
        services.AddScoped<IJwtService,                    JwtService>();
        services.AddScoped<IChannelConfigService,          ChannelConfigService>();
        services.AddScoped<IFallbackPolicyService,         FallbackPolicyService>();
        services.AddScoped<IFallbackHandlerService,        FallbackHandlerService>();
        services.AddScoped<IChannelDispatchService,        ChannelDispatchService>();
        services.AddScoped<ISendTextOrchestratorService,   SendTextOrchestratorService>();
        services.AddScoped<IManualSendService,             ManualSendService>();
        services.AddScoped<IDashboardService,              DashboardService>();
        services.AddScoped<IApiLogService,                 ApiLogService>();
        services.AddScoped<IAdminUserService,              AdminUserService>();
        services.AddScoped<IVoiceReportService,            VoiceReportService>();
        return services;
    }

    // ── JWT Auth ──────────────────────────────────────────────────────────
    public static IServiceCollection AddJwtAuth(
        this IServiceCollection services, IConfiguration config)
    {
        var key = Encoding.UTF8.GetBytes(config["Jwt:Key"]!);

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey         = new SymmetricSecurityKey(key),
                    ValidateIssuer           = true,
                    ValidIssuer              = config["Jwt:Issuer"],
                    ValidateAudience         = true,
                    ValidAudience            = config["Jwt:Audience"],
                    ValidateLifetime         = true,
                    ClockSkew                = TimeSpan.Zero
                };
            });

        services.AddAuthorization();
        return services;
    }

    // ── CORS (origins loaded from DB at startup + appsettings fallback) ───
    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services, IConfiguration config)
    {
        // Static origins from config; DB-loaded origins added in Program.cs after seed
        var configOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>()
                            ?? ["http://localhost:4200"];

        services.AddCors(opts => opts.AddPolicy("DefaultCors", p =>
            p.WithOrigins(configOrigins)
             .AllowAnyMethod()
             .AllowAnyHeader()
             .AllowCredentials()));

        return services;
    }

    // ── Centralized Polly resilience config (driven by appsettings) ───────
    private static void ConfigureResilience(
        HttpStandardResilienceOptions opts, int retryCount, int retryDelaySeconds)
    {
        opts.Retry.MaxRetryAttempts = retryCount;
        opts.Retry.BackoffType      = Polly.DelayBackoffType.Exponential;
        opts.Retry.UseJitter        = true;
        opts.Retry.Delay            = TimeSpan.FromSeconds(retryDelaySeconds);

        opts.CircuitBreaker.SamplingDuration  = TimeSpan.FromSeconds(30);
        opts.CircuitBreaker.MinimumThroughput = 5;
        opts.CircuitBreaker.FailureRatio      = 0.5;
        opts.CircuitBreaker.BreakDuration     = TimeSpan.FromSeconds(15);

        opts.AttemptTimeout.Timeout      = TimeSpan.FromSeconds(25);
        opts.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(90);
    }
}
