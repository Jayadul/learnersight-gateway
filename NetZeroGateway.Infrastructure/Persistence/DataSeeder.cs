using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using System.Text.Json;

namespace NetZeroGateway.Infrastructure.Persistence;

/// <summary>
/// Idempotent seeder — runs on every startup.
/// Strategy per entity:
///   - ADD  if the record does not exist (matched by natural key)
///   - UPDATE if it exists but config-driven values have changed
///   - SKIP  if nothing has changed (no DB write at all)
/// Passwords and secrets are NEVER re-hashed on update unless the raw
/// value in config has changed — detected via BCrypt.Verify.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider sp)
    {
        var db = sp.GetRequiredService<NetZeroGatewayDbContext>();
        var enc = sp.GetRequiredService<IEncryptionService>();
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILogger<NetZeroGatewayDbContext>>();

        logger.LogInformation("[SEED] Starting idempotent seed check");

        await SeedAdminUsersAsync(db, config, logger);
        await SeedChannelConfigsAsync(db, enc, config, logger);
        await SeedFallbackPoliciesAsync(db, config, logger);
        await SeedGatewayCredentialsAsync(db, enc, config, logger);

        logger.LogInformation("[SEED] Seed check complete");
    }

    // ── Admin Users ───────────────────────────────────────────────────────
    private static async Task SeedAdminUsersAsync(
        NetZeroGatewayDbContext db,
        IConfiguration config,
        ILogger logger)
    {
        var users = config.GetSection("Seed:AdminUsers").Get<List<SeedAdminUser>>() ?? [];

        foreach (var cfg in users)
        {
            var existing = await db.AdminUsers
                .FirstOrDefaultAsync(u => u.Username == cfg.Username);

            if (existing is null)
            {
                db.AdminUsers.Add(new AdminUser
                {
                    Username = cfg.Username,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(cfg.Password),
                    Email = cfg.Email,
                    Role = Enum.Parse<AdminRole>(cfg.Role),
                    IsActive = true
                });
                logger.LogInformation("[SEED] AdminUser ADD: {Username}", cfg.Username);
            }
            else
            {
                var changed = false;

                // Only rehash password if the raw config value no longer matches the stored hash
                if (!BCrypt.Net.BCrypt.Verify(cfg.Password, existing.PasswordHash))
                {
                    existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(cfg.Password);
                    changed = true;
                    logger.LogInformation("[SEED] AdminUser UPDATE password: {Username}", cfg.Username);
                }

                if (existing.Email != cfg.Email)
                {
                    existing.Email = cfg.Email;
                    changed = true;
                }

                var role = Enum.Parse<AdminRole>(cfg.Role);
                if (existing.Role != role)
                {
                    existing.Role = role;
                    changed = true;
                }

                if (changed)
                {
                    db.AdminUsers.Update(existing);
                    logger.LogInformation("[SEED] AdminUser UPDATE: {Username}", cfg.Username);
                }
                else
                {
                    logger.LogDebug("[SEED] AdminUser SKIP (no change): {Username}", cfg.Username);
                }
            }
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Parses a seeded channel-type name, returning null for anything unrecognised.
    /// Seeding runs at boot, so throwing here would stop the whole gateway starting over
    /// a typo in a config file — the caller logs the bad entry and skips it instead.
    /// Accepts the pre-unification WP_Text/WP_Media names, and the friendly names the
    /// admin UI displays: someone editing this file reads "WhatsApp" on screen, not "WP".
    /// </summary>
    private static ChannelType? ParseChannelType(string name)
    {
        var trimmed = name?.Trim() ?? "";

        return trimmed.ToLowerInvariant() switch
        {
            "wp" or "wp_text" or "wp_media" or "whatsapp" => ChannelType.WP,
            "sms" => ChannelType.SMS,
            "voice" or "voice call" or "call" => ChannelType.Voice,
            _ => Enum.TryParse<ChannelType>(trimmed, ignoreCase: true, out var parsed)
                ? parsed
                : null
        };
    }

    /// <summary>
    /// Lowercase alphanumeric slug derived from a label, made unique within a channel
    /// type by a numeric suffix. Hyphens are stripped: the combined tag form splits on
    /// "-wp_label:", so a hyphen inside a slug would make it ambiguous to parse.
    /// </summary>
    private static string UniqueSlug(string label, IEnumerable<string> taken)
    {
        var baseSlug = new string(label.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
        if (baseSlug.Length == 0) baseSlug = "channel";

        var used = taken.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!used.Contains(baseSlug)) return baseSlug;

        for (var n = 2; ; n++)
        {
            var candidate = $"{baseSlug}{n}";
            if (!used.Contains(candidate)) return candidate;
        }
    }

    // ── Channel Configs ───────────────────────────────────────────────────
    private static async Task SeedChannelConfigsAsync(
        NetZeroGatewayDbContext db,
        IEncryptionService enc,
        IConfiguration config,
        ILogger logger)
    {
        var items = config.GetSection("Seed:ChannelConfigs")
            .Get<List<SeedChannelConfig>>() ?? [];

        foreach (var item in items)
        {
            if (ParseChannelType(item.ChannelType) is not { } channelType)
            {
                logger.LogError(
                    "[SEED] ChannelConfig SKIP: '{Label}' names channel type '{Type}', which "
                    + "does not exist. Use SMS, WP or Voice. The channel was not created.",
                    item.Label, item.ChannelType);
                continue;
            }

            var plainJson = JsonSerializer.Serialize(item.Config,
                new JsonSerializerOptions { WriteIndented = false });

            var existing = await db.ChannelConfigs
                .FirstOrDefaultAsync(c => c.ChannelType == channelType
                                       && c.Label == item.Label);

            if (existing is null)
            {
                // Includes entries added earlier in this same pass: they are not persisted
                // until SaveChanges, so a DB-only query would hand out duplicate slugs.
                var siblings = (await db.ChannelConfigs
                        .Where(c => c.ChannelType == channelType).ToListAsync())
                    .Concat(db.ChannelConfigs.Local.Where(c => c.ChannelType == channelType))
                    .ToList();

                db.ChannelConfigs.Add(new ChannelConfig
                {
                    ChannelType = channelType,
                    ProviderName = item.ProviderName,
                    Label = item.Label,
                    LabelSlug = UniqueSlug(item.Label, siblings.Select(c => c.LabelSlug)),
                    IsActive = true,
                    // First config of a type becomes its default; later ones are added
                    // active but non-default so they never silently steal live traffic.
                    IsDefault = !siblings.Any(c => c.IsDefault),
                    ConfigJson = enc.Encrypt(plainJson)
                });
                logger.LogInformation("[SEED] ChannelConfig ADD: {Type} / {Label}",
                    channelType, item.Label);
            }
            else
            {
                var changed = false;

                // Decrypt existing to compare — only re-encrypt if config actually changed
                string existingPlain;
                try { existingPlain = enc.Decrypt(existing.ConfigJson); }
                catch { existingPlain = ""; }

                // Normalize both sides before comparing
                var normalizedNew = NormalizeJson(plainJson);
                var normalizedExisting = NormalizeJson(existingPlain);

                if (normalizedExisting != normalizedNew)
                {
                    existing.ConfigJson = enc.Encrypt(plainJson);
                    changed = true;
                }

                if (existing.ProviderName != item.ProviderName)
                {
                    existing.ProviderName = item.ProviderName;
                    changed = true;
                }

                if (changed)
                {
                    db.ChannelConfigs.Update(existing);
                    logger.LogInformation("[SEED] ChannelConfig UPDATE: {Type} / {Label}",
                        item.ChannelType, item.Label);
                }
                else
                {
                    logger.LogDebug("[SEED] ChannelConfig SKIP (no change): {Type} / {Label}",
                        item.ChannelType, item.Label);
                }
            }
        }

        await db.SaveChangesAsync();
    }

    // ── Fallback Policies ─────────────────────────────────────────────────
    private static async Task SeedFallbackPoliciesAsync(
        NetZeroGatewayDbContext db,
        IConfiguration config,
        ILogger logger)
    {
        var items = config.GetSection("Seed:FallbackPolicies")
            .Get<List<SeedFallbackPolicy>>() ?? [];

        foreach (var item in items)
        {
            if (ParseChannelType(item.TriggerChannel) is not { } triggerChannel
                || ParseChannelType(item.FallbackChannel) is not { } fallbackChannel)
            {
                logger.LogError(
                    "[SEED] FallbackPolicy SKIP: '{Name}' names channel type '{Trigger}' or "
                    + "'{Fallback}', which does not exist. Use SMS, WP or Voice.",
                    item.Name, item.TriggerChannel, item.FallbackChannel);
                continue;
            }

            // Crossing types needs the opt-in, and the seeder used not to carry it — so a
            // seeded cross-type rule was created looking active and then skipped at every
            // dispatch, which reads as a broken rule rather than a disabled one.
            if (triggerChannel != fallbackChannel && !item.AllowCrossChannel)
            {
                logger.LogWarning(
                    "[SEED] FallbackPolicy SKIP: '{Name}' retries {Trigger} on {Fallback}, which "
                    + "crosses channel types. Set AllowCrossChannel to seed it — crossing types "
                    + "changes both the delivery format and the cost of the message.",
                    item.Name, triggerChannel, fallbackChannel);
                continue;
            }

            // A WP_Text -> WP_Media policy collapses to WP -> WP, which would make a
            // channel fall back to itself. Skip rather than seed a nonsensical rule.
            if (triggerChannel == fallbackChannel)
            {
                logger.LogWarning(
                    "[SEED] FallbackPolicy SKIP: {Name} — trigger and fallback are both {Channel} "
                    + "after the WhatsApp unification", item.Name, triggerChannel);
                continue;
            }

            var existing = await db.FallbackPolicies
                .FirstOrDefaultAsync(p => p.Name == item.Name);

            if (existing is null)
            {
                db.FallbackPolicies.Add(new FallbackPolicy
                {
                    Name = item.Name,
                    TriggerChannel = triggerChannel,
                    FallbackChannel = fallbackChannel,
                    AllowCrossChannel = item.AllowCrossChannel,
                    IsActive = item.IsActive,
                    IsGlobal = item.IsGlobal,
                    Description = item.Description
                });
                logger.LogInformation("[SEED] FallbackPolicy ADD: {Name}", item.Name);
            }
            else
            {
                // An existing policy is left exactly as it is. The seed decides what a
                // fresh database starts with; after that the rule belongs to whoever
                // administers it. Re-applying the seeded values here meant switching a
                // rule off in the admin panel lasted only until the next restart — and
                // for a cross-channel rule, that silently resumed paying twice per send.
                logger.LogDebug(
                    "[SEED] FallbackPolicy SKIP (already exists, admin owns it): {Name}",
                    item.Name);
            }
        }

        await db.SaveChangesAsync();
    }

    // ── Gateway Credentials ───────────────────────────────────────────────

    /// <summary>
    /// Create-only. Unlike a channel config, these keys are handed to third parties and
    /// then rotated in the admin panel; re-asserting them from config on every restart
    /// would silently roll a rotation back and lock every integrator out until someone
    /// noticed. The seed decides what a fresh database starts with and nothing more.
    /// </summary>
    private static async Task SeedGatewayCredentialsAsync(
        NetZeroGatewayDbContext db,
        IEncryptionService enc,
        IConfiguration config,
        ILogger logger)
    {
        if (await db.GatewayCredentials.AnyAsync())
        {
            logger.LogDebug("[SEED] GatewayCredential SKIP (already exists, admin owns it)");
            return;
        }

        var seed = config.GetSection("Seed:GatewayCredentials").Get<SeedGatewayCredential>();

        if (seed is null
            || string.IsNullOrWhiteSpace(seed.Username)
            || string.IsNullOrWhiteSpace(seed.ApiKey)
            || string.IsNullOrWhiteSpace(seed.SecretKey))
        {
            // Not fatal, but the gateway rejects every /sendtext call until someone fixes
            // it, so this is an error rather than a note.
            logger.LogError(
                "[SEED] GatewayCredential SKIP: Seed:GatewayCredentials is missing Username, "
                + "ApiKey or SecretKey. /sendtext will reject every request until credentials "
                + "exist. Add the section to appsettings and restart.");
            return;
        }

        db.GatewayCredentials.Add(new GatewayCredential
        {
            Username = seed.Username.Trim(),
            ApiKeyEncrypted = enc.Encrypt(seed.ApiKey.Trim()),
            SecretKeyEncrypted = enc.Encrypt(seed.SecretKey.Trim())
        });

        // The keys themselves are never logged.
        logger.LogInformation("[SEED] GatewayCredential ADD: {Username}", seed.Username);

        await db.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Deserialize and re-serialize JSON to get a canonical form for comparison.
    /// Prevents false positives from whitespace or key-ordering differences.
    /// </summary>
    private static string NormalizeJson(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(doc,
                new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return json;
        }
    }
}

// ── Config binding models (local to this file) ────────────────────────────

file sealed class SeedAdminUser
{
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "Admin";
}

file sealed class SeedChannelConfig
{
    public string ChannelType { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string Label { get; set; } = "";
    public Dictionary<string, string> Config { get; set; } = [];
}

file sealed class SeedGatewayCredential
{
    public string Username { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
}

file sealed class SeedFallbackPolicy
{
    public string Name { get; set; } = "";
    public string TriggerChannel { get; set; } = "";
    public string FallbackChannel { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsGlobal { get; set; }
    /// <summary>Required for a rule whose two sides are different channel types.</summary>
    public bool AllowCrossChannel { get; set; }
    public string? Description { get; set; }
}