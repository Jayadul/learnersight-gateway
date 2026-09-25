using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Infrastructure.Balances;
using NetZeroGateway.Infrastructure.HttpClients;
using NetZeroGateway.Infrastructure.Persistence;
using NetZeroGateway.Infrastructure.Repositories;
using NetZeroGateway.Infrastructure.Security;
using NetZeroGateway.Infrastructure.Services;
using NetZeroGateway.Infrastructure.StatusSync;
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
        services.AddScoped<IDispatchJobRecipientRepository, DispatchJobRecipientRepository>();
        services.AddScoped<IResendAttemptRepository, ResendAttemptRepository>();
        services.AddScoped<IApiLogRepository, ApiLogRepository>();
        services.AddScoped<IFallbackPolicyRepository, FallbackPolicyRepository>();
        services.AddScoped<IMediaFileRepository, MediaFileRepository>();
        services.AddScoped<IGatewayCredentialRepository, GatewayCredentialRepository>();

        // Media file storage (local disk / Docker volume)
        services.AddScoped<IMediaStorageService, LocalMediaStorageService>();

        // Encryption
        services.AddScoped<IEncryptionService, AesEncryptionService>();

        // Backup export (admin xlsx download)
        services.AddScoped<IBackupExportService, BackupExportService>();

        // HTTP clients — base URLs + Polly from config
        var retryCount = config.GetValue<int>(ConfigKeys.RetryMaxAttempts, 3);
        var retryDelay = config.GetValue<int>(ConfigKeys.RetryDelaySeconds, 2);
        var timeout = config.GetValue<int>(ConfigKeys.TimeoutSeconds, 30);
        // WhatsApp (watrend) processes a real send inline and can take ~60s+ to respond,
        // so it needs a much longer timeout than the other providers.
        var waTimeout = config.GetValue<int>(ConfigKeys.WhatsAppTimeoutSeconds, 120);

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
                c.Timeout = TimeSpan.FromSeconds(waTimeout + 20);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay, waTimeout));

        services
            .AddHttpClient<INetZeroVoiceClient, NetZeroVoiceClient>(c =>
            {
                c.BaseAddress = new Uri(config[ConfigKeys.VoiceBaseUrl]!);
                c.Timeout = TimeSpan.FromSeconds(timeout);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay));

        // The Release 3 providers. Their base URLs live on the channel config (ADR 0008),
        // so the registered BaseAddress is only the fallback default for a channel that
        // leaves the field blank.
        services
            .AddHttpClient<IChithiClient, ChithiClient>(c =>
            {
                c.BaseAddress = new Uri(
                    config[ConfigKeys.ChithiBaseUrl] ?? "https://sms.chithi.net");
                c.Timeout = TimeSpan.FromSeconds(timeout);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay));

        services
            .AddHttpClient<INetZeroInboxClient, NetZeroInboxClient>(c =>
            {
                c.BaseAddress = new Uri(
                    config[ConfigKeys.NetZeroInboxBaseUrl] ?? "https://inbox.netzero.com.bd");
                // WhatsApp sends can sit while the provider works, as WaTrend does.
                c.Timeout = TimeSpan.FromSeconds(waTimeout + 20);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay, waTimeout));

        services
            .AddHttpClient<INetZeroInboxQrClient, NetZeroInboxQrClient>(c =>
            {
                // https, despite the vendor documenting http: the host 301s to https,
                // and this endpoint carries its token in the query string, so the plain
                // request would put a credential on the wire in the clear before the
                // redirect. Following a redirect is not a substitute for not sending it.
                c.BaseAddress = new Uri(
                    config[ConfigKeys.NetZeroInboxQrBaseUrl] ?? "https://inbox.netzero.com.bd");
                c.Timeout = TimeSpan.FromSeconds(waTimeout + 20);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay, waTimeout));

        // REVE's balance sits on a different host and scheme from its send endpoint
        // (https, no port), so it gets its own client rather than reusing the send one.
        services
            .AddHttpClient<IReveBalanceClient, ReveBalanceClient>(c =>
            {
                c.BaseAddress = new Uri("https://smpp.revesms.com");
                c.Timeout = TimeSpan.FromSeconds(timeout);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay));

        // Status lookups. REVE and Chithi reuse their send host and credentials; the
        // voice report is on the voice host — a channel type never implies a sync exists.
        services
            .AddHttpClient<IReveStatusClient, ReveStatusClient>(c =>
            {
                c.BaseAddress = new Uri(config[ConfigKeys.SmsBaseUrl]!);
                c.Timeout = TimeSpan.FromSeconds(timeout);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay));

        services
            .AddHttpClient<IChithiStatusClient, ChithiStatusClient>(c =>
            {
                c.BaseAddress = new Uri(
                    config[ConfigKeys.ChithiBaseUrl] ?? "https://sms.chithi.net");
                c.Timeout = TimeSpan.FromSeconds(timeout);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay));

        services
            .AddHttpClient<INetZeroVoiceReportClient, NetZeroVoiceReportClient>(c =>
            {
                c.BaseAddress = new Uri(config[ConfigKeys.VoiceBaseUrl]!);
                // The report pages over a whole day and is slower than a send.
                c.Timeout = TimeSpan.FromSeconds(timeout * 2);
            })
            .AddStandardResilienceHandler(o => Resilience(o, retryCount, retryDelay));

        services.AddScoped<IChannelStatusSyncProvider, ReveStatusSyncProvider>();
        services.AddScoped<IChannelStatusSyncProvider, ChithiStatusSyncProvider>();
        services.AddScoped<IChannelStatusSyncProvider, NetZeroVoiceStatusSyncProvider>();

        // Balance readers. Not every provider has one, and a channel type never implies
        // that it does — Voice and WhatsApp report no balance at all.
        services.AddScoped<IChannelBalanceProvider, ReveBalanceProvider>();
        services.AddScoped<IChannelBalanceProvider, ChithiBalanceProvider>();

        // Channel sender strategies. One per (ChannelType, Provider, HandlesMedia) —
        // the factory rejects a duplicate key at startup, so adding a second provider for
        // a channel type is additive here. See ADR 0008.
        services.AddScoped<IChannelSender, SmsSender>();            // SMS  / Reve
        services.AddScoped<IChannelSender, WhatsAppTextSender>();   // WP   / WaTrend
        services.AddScoped<IChannelSender, WhatsAppMediaSender>();  // WP   / WaTrend + media
        services.AddScoped<IChannelSender, VoiceSender>();          // Voice/ NetZeroVoice
        services.AddScoped<IChannelSender, ChithiSmsSender>();      // SMS  / Chithi
        services.AddScoped<IChannelSender, NetZeroInboxWhatsAppSender>();   // WP / NetZeroInbox v1
        services.AddScoped<IChannelSender, NetZeroInboxQrWhatsAppSender>(); // WP / NetZeroInbox QR
        services.AddScoped<IChannelSender, NetZeroInboxTemplateOnlySender>(); // WP / NetZeroInbox Template (refuses text)
        services.AddScoped<IWhatsAppTemplateSender, NetZeroInboxTemplateSender>();
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
        services.AddScoped<IChannelBalanceService, ChannelBalanceService>();
        services.AddScoped<IStatusSyncService, StatusSyncService>();
        services.AddScoped<IResendService, ResendService>();
        services.AddScoped<IDispatchJobRecipientService, DispatchJobRecipientService>();
        services.AddScoped<IFallbackPolicyService, FallbackPolicyService>();
        services.AddScoped<IFallbackHandlerService, FallbackHandlerService>();
        services.AddScoped<IChannelDispatchService, ChannelDispatchService>();
        services.AddScoped<ISendTextOrchestratorService, SendTextOrchestratorService>();
        services.AddScoped<IManualSendService, ManualSendService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IApiLogService, ApiLogService>();
        services.AddScoped<IAdminUserService, AdminUserService>();
        services.AddScoped<IMediaLibraryService, MediaLibraryService>();
        services.AddScoped<IGatewayCredentialService, GatewayCredentialService>();
        return services;
    }

    // ── Background jobs (Hangfire, same Postgres database) ────────────────
    public static IServiceCollection AddBackgroundJobs(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddHangfire(h => h
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(o =>
                o.UseNpgsqlConnection(config.GetConnectionString("DefaultConnection"))));
        services.AddHangfireServer();
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
        HttpStandardResilienceOptions o, int retryCount, int retryDelay,
        int attemptTimeoutSeconds = 25)
    {
        o.Retry.MaxRetryAttempts = retryCount;
        o.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
        o.Retry.UseJitter = true;
        o.Retry.Delay = TimeSpan.FromSeconds(retryDelay);
        o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(attemptTimeoutSeconds);
        // Polly requires SamplingDuration >= 2x AttemptTimeout and TotalRequestTimeout >= AttemptTimeout.
        o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(Math.Max(60, attemptTimeoutSeconds * 2));
        o.CircuitBreaker.MinimumThroughput = 5;
        o.CircuitBreaker.FailureRatio = 0.5;
        o.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(Math.Max(120, attemptTimeoutSeconds + 10));
    }
}