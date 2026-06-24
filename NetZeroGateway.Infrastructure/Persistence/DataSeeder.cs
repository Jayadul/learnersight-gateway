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
            var channelType = Enum.Parse<ChannelType>(item.ChannelType);
            var plainJson = JsonSerializer.Serialize(item.Config,
                new JsonSerializerOptions { WriteIndented = false });

            var existing = await db.ChannelConfigs
                .FirstOrDefaultAsync(c => c.ChannelType == channelType
                                       && c.Label == item.Label);

            if (existing is null)
            {
                db.ChannelConfigs.Add(new ChannelConfig
                {
                    ChannelType = channelType,
                    ProviderName = item.ProviderName,
                    Label = item.Label,
                    IsActive = true,
                    ConfigJson = enc.Encrypt(plainJson)
                });
                logger.LogInformation("[SEED] ChannelConfig ADD: {Type} / {Label}",
                    item.ChannelType, item.Label);
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
            var triggerChannel = Enum.Parse<ChannelType>(item.TriggerChannel);
            var fallbackChannel = Enum.Parse<ChannelType>(item.FallbackChannel);

            var existing = await db.FallbackPolicies
                .FirstOrDefaultAsync(p => p.Name == item.Name);

            if (existing is null)
            {
                db.FallbackPolicies.Add(new FallbackPolicy
                {
                    Name = item.Name,
                    TriggerChannel = triggerChannel,
                    FallbackChannel = fallbackChannel,
                    IsActive = item.IsActive,
                    IsGlobal = item.IsGlobal,
                    Description = item.Description
                });
                logger.LogInformation("[SEED] FallbackPolicy ADD: {Name}", item.Name);
            }
            else
            {
                var changed = false;

                if (existing.TriggerChannel != triggerChannel) { existing.TriggerChannel = triggerChannel; changed = true; }
                if (existing.FallbackChannel != fallbackChannel) { existing.FallbackChannel = fallbackChannel; changed = true; }
                if (existing.IsActive != item.IsActive) { existing.IsActive = item.IsActive; changed = true; }
                if (existing.IsGlobal != item.IsGlobal) { existing.IsGlobal = item.IsGlobal; changed = true; }
                if (existing.Description != item.Description) { existing.Description = item.Description; changed = true; }

                if (changed)
                {
                    db.FallbackPolicies.Update(existing);
                    logger.LogInformation("[SEED] FallbackPolicy UPDATE: {Name}", item.Name);
                }
                else
                {
                    logger.LogDebug("[SEED] FallbackPolicy SKIP (no change): {Name}", item.Name);
                }
            }
        }

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

file sealed class SeedFallbackPolicy
{
    public string Name { get; set; } = "";
    public string TriggerChannel { get; set; } = "";
    public string FallbackChannel { get; set; } = "";
    public bool IsActive { get; set; }
    public bool IsGlobal { get; set; }
    public string? Description { get; set; }
}