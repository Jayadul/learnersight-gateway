using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Infrastructure.Persistence;

public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db     = sp.GetRequiredService<NetZeroGatewayDbContext>();
        var enc    = sp.GetRequiredService<IEncryptionService>();
        var logger = sp.GetRequiredService<ILogger<NetZeroGatewayDbContext>>();

        if (await db.AdminUsers.AnyAsync()) return;

        logger.LogInformation("[SEED] No data found — seeding initial data");

        // ── Admin users ───────────────────────────────────────────────────
        db.AdminUsers.AddRange(
            new AdminUser
            {
                Username     = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                Email        = "admin@netzero.com",
                Role         = AdminRole.SuperAdmin,
                IsActive     = true
            },
            new AdminUser
            {
                Username     = "operator",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Operator@123"),
                Email        = "operator@netzero.com",
                Role         = AdminRole.Admin,
                IsActive     = true
            });

        // ── XSoft API key ─────────────────────────────────────────────────
        const string rawApiKey    = "xsoft_api_key_default";
        const string rawSecretKey = "xsoft_secret_default";
        db.XSoftApiKeys.Add(new XSoftApiKey
        {
            Label         = "x-soft-default",
            ApiKeyHash    = BCrypt.Net.BCrypt.HashPassword(rawApiKey),
            SecretKeyHash = BCrypt.Net.BCrypt.HashPassword(rawSecretKey),
            IsActive      = true
        });

        // ── XSoft App Config (base URL + shared secret for header auth) ───
        const string rawXSoftSecret = "xsoft_shared_secret_default";
        db.XSoftAppConfigs.Add(new XSoftAppConfig
        {
            Label            = "x-soft",
            BaseUrl          = "http://localhost:5050",   // update from admin panel
            SharedSecretHash = BCrypt.Net.BCrypt.HashPassword(rawXSoftSecret),
            IsActive         = true,
            Description      = "ASP.NET MVC 4.2 calling application"
        });

        Console.WriteLine($"[SEED] XSoft API Key      : {rawApiKey}");
        Console.WriteLine($"[SEED] XSoft Secret Key   : {rawSecretKey}");
        Console.WriteLine($"[SEED] XSoft Header Token : {rawXSoftSecret}");
        logger.LogInformation("[SEED] XSoft credentials printed to console (shown once only)");

        // ── Channel configs — generic labels, actual provider keys as placeholders ──
        var smsJson = """
            {
              "apikey":    "REPLACE_WITH_SMS_PROVIDER_API_KEY",
              "secretkey": "REPLACE_WITH_SMS_PROVIDER_SECRET",
              "callerID":  "REPLACE_WITH_CALLER_ID",
              "baseUrl":   "http://smpp.revesms.com:7788"
            }
            """;

        var wpTextJson = """
            {
              "instance_id":  "REPLACE_WITH_INSTANCE_ID",
              "access_token": "REPLACE_WITH_ACCESS_TOKEN",
              "baseUrl":      "https://app.watrend.com"
            }
            """;

        var wpMediaJson = """
            {
              "instance_id":  "REPLACE_WITH_INSTANCE_ID",
              "access_token": "REPLACE_WITH_ACCESS_TOKEN",
              "baseUrl":      "https://app.watrend.com"
            }
            """;

        var voiceJson = """
            {
              "owner":      "REPLACE_WITH_OWNER",
              "password":   "REPLACE_WITH_PASSWORD",
              "authUserId": "REPLACE_WITH_AUTH_USER_ID",
              "lotNumber":  "1",
              "campaignId": "REPLACE_WITH_CAMPAIGN_ID",
              "baseUrl":    "http://api.sms.netzero.com.bd:8000"
            }
            """;

        db.ChannelConfigs.AddRange(
            new ChannelConfig
            {
                ChannelType  = ChannelType.SMS,
                ProviderName = "SMS Provider",
                Label        = "SMS Default",
                IsActive     = true,
                ConfigJson   = enc.Encrypt(smsJson.Trim())
            },
            new ChannelConfig
            {
                ChannelType  = ChannelType.WP_Text,
                ProviderName = "WhatsApp Provider",
                Label        = "WhatsApp Text Default",
                IsActive     = true,
                ConfigJson   = enc.Encrypt(wpTextJson.Trim())
            },
            new ChannelConfig
            {
                ChannelType  = ChannelType.WP_Media,
                ProviderName = "WhatsApp Provider",
                Label        = "WhatsApp Media Default",
                IsActive     = true,
                ConfigJson   = enc.Encrypt(wpMediaJson.Trim())
            },
            new ChannelConfig
            {
                ChannelType  = ChannelType.Voice,
                ProviderName = "Voice Provider",
                Label        = "Voice Campaign Default",
                IsActive     = true,
                ConfigJson   = enc.Encrypt(voiceJson.Trim())
            });

        // ── Fallback policies ─────────────────────────────────────────────
        db.FallbackPolicies.AddRange(
            new FallbackPolicy
            {
                Name            = "WhatsApp fails → SMS",
                TriggerChannel  = ChannelType.WP_Text,
                FallbackChannel = ChannelType.SMS,
                IsActive        = true,
                IsGlobal        = true,
                Description     = "Fallback to SMS when WhatsApp text delivery fails"
            },
            new FallbackPolicy
            {
                Name            = "Voice fails → SMS",
                TriggerChannel  = ChannelType.Voice,
                FallbackChannel = ChannelType.SMS,
                IsActive        = false,
                IsGlobal        = true,
                Description     = "Fallback to SMS when voice call fails"
            });

        await db.SaveChangesAsync();
        logger.LogInformation("[SEED] Seed complete — {Count} entities created", 11);
    }
}
