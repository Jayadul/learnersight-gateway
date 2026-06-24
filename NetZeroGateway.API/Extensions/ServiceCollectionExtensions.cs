using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.HttpClients;
using NetZeroGateway.Infrastructure.Persistence;
using NetZeroGateway.Infrastructure.Repositories;
using NetZeroGateway.Infrastructure.Security;
using NetZeroGateway.Infrastructure.Strategies;
using System.Text;

namespace NetZeroGateway.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IMvcBuilder AddControllersWithAuthPolicy(
        this IServiceCollection services, IConfiguration config)
        => services.AddControllers(opts =>
            opts.Conventions.Add(new AuthorizationPolicyConvention(config)));

    // ── Infrastructure ────────────────────────────────────────────────────
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration config)
    {
        // EF Core
        services.AddDbContext<NetZeroGatewayDbContext>(opts =>
            opts.UseNpgsql(config.GetConnectionString("DefaultConnection"))
                .UseSnakeCaseNamingConvention());

        // Repositories
        services.AddScoped<IAdminUserRepository, AdminUserRepository>();
        services.AddScoped<IChannelConfigRepository, ChannelConfigRepository>();
        services.AddScoped<IMessageRequestRepository, MessageRequestRepository>();
        services.AddScoped<IDispatchJobRepository, DispatchJobRepository>();
        services.AddScoped<IApiLogRepository, ApiLogRepository>();
        services.AddScoped<IFallbackPolicyRepository, FallbackPolicyRepository>();

        // Encryption + voice gateway
        services.AddScoped<IEncryptionService, AesEncryptionService>();
        services.AddScoped<IVoiceReportGateway, VoiceReportGateway>();

        // HTTP clients — base URLs + Polly from config
        var retryCount = config.GetValue<int>(ConfigKeys.RetryMaxAttempts, 3);
        var retryDelay = config.GetValue<int>(ConfigKeys.RetryDelaySeconds, 2);
        var timeout = config.GetValue<int>(ConfigKeys.TimeoutSeconds, 30);
        var rptTimeout = config.GetValue<int>(ConfigKeys.ReportTimeoutSeconds, 60);

        services
            .AddHttpClient<IReveSmsClient, ReveSmsClient>(c =>
            {
                c.BaseAddress = new Uri(config[ConfigKeys.SmsBaseUrl]!);
                c.Timeout = TimeSpan.FromSeconds(timeout);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay));

        services
            .AddHttpClient<IWaTrendClient, WaTrendClient>(c =>
            {
                c.BaseAddress = new Uri(config[ConfigKeys.WhatsAppBaseUrl]!);
                c.Timeout = TimeSpan.FromSeconds(timeout);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay));

        services
            .AddHttpClient<INetZeroVoiceClient, NetZeroVoiceClient>(c =>
            {
                c.BaseAddress = new Uri(config[ConfigKeys.VoiceBaseUrl]!);
                c.Timeout = TimeSpan.FromSeconds(timeout);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay));

        services
            .AddHttpClient<INetZeroReportClient, NetZeroReportClient>(c =>
            {
                c.BaseAddress = new Uri(config[ConfigKeys.VoiceReportBaseUrl]!);
                c.Timeout = TimeSpan.FromSeconds(rptTimeout);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay));

        // Channel sender strategies
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
        services.AddScoped<IEncryptionService, AesEncryptionService>();
        services.AddScoped<ITemplateParserService, TemplateParserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IChannelConfigService, ChannelConfigService>();
        services.AddScoped<IFallbackPolicyService, FallbackPolicyService>();
        services.AddScoped<IFallbackHandlerService, FallbackHandlerService>();
        services.AddScoped<IChannelDispatchService, ChannelDispatchService>();
        services.AddScoped<ISendTextOrchestratorService, SendTextOrchestratorService>();
        services.AddScoped<IManualSendService, ManualSendService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IApiLogService, ApiLogService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IVoiceReportService, VoiceReportService>();
        return services;
    }

    // ── JWT ───────────────────────────────────────────────────────────────
    public static IServiceCollection AddJwtAuth(
        this IServiceCollection services, IConfiguration config)
    {
        var key = Encoding.UTF8.GetBytes(config[ConfigKeys.JwtKey]!);
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = config[ConfigKeys.JwtIssuer],
                    ValidateAudience = true,
                    ValidAudience = config[ConfigKeys.JwtAudience],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });
        services.AddAuthorization();
        return services;
    }

    // ── CORS ──────────────────────────────────────────────────────────────
    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services, IConfiguration config)
    {
        var origins = config.GetSection(ConfigKeys.CorsAllowedOrigins).Get<string[]>()
                         ?? ["http://localhost:4200"];
        var policyName = config[ConfigKeys.AdminCorsPolicy] ?? "DefaultCors";

        services.AddCors(opts => opts.AddPolicy(policyName, p =>
            p.WithOrigins(origins)
             .AllowAnyMethod()
             .AllowAnyHeader()
             .AllowCredentials()));

        return services;
    }

    // ── Polly ─────────────────────────────────────────────────────────────
    private static void Resilience(
        HttpStandardResilienceOptions o, int retryCount, int retryDelay)
    {
        o.Retry.MaxRetryAttempts = retryCount;
        o.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        o.Retry.UseJitter = true;
        o.Retry.Delay = TimeSpan.FromSeconds(retryDelay);
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(25);
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(60);
        o.CircuitBreaker.MinimumThroughput = 5;
        o.CircuitBreaker.FailureRatio = 0.5;
        o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);
    }
}