using System.Text.Json;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class ChannelConfigService(
    IChannelConfigRepository repo,
    IEncryptionService enc
) : IChannelConfigService
{
    public async Task<List<ChannelConfigDto>> GetAllAsync(ChannelType? filter)
    {
        var list = filter is null
            ? await repo.GetAllAsync()
            : await repo.GetAllByChannelTypeAsync(filter.Value);
        return list.Select(ToDto).ToList();
    }

    public async Task<ChannelConfigDto> GetByIdAsync(Guid id)
        => ToDto(await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found."));

    public async Task<ChannelConfigDto> CreateAsync(CreateChannelConfigRequest req)
    {
        var siblings = await repo.GetAllByChannelTypeAsync(req.ChannelType);
        var entity = new ChannelConfig
        {
            ChannelType = req.ChannelType,
            ProviderName = req.ProviderName,
            Label = req.Label,
            LabelSlug = await ResolveSlugAsync(req.ChannelType, req.LabelSlug, req.Label, null),
            ConfigJson = enc.Encrypt(req.ConfigJson),
            IsActive = false,
            // The first config of a type becomes its default; otherwise the operator
            // promotes one explicitly, so creating a channel never silently reroutes
            // live traffic away from the current default.
            IsDefault = siblings.Count == 0
        };
        if (entity.IsDefault) entity.IsActive = true;

        await repo.AddAsync(entity);
        await repo.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<ChannelConfigDto> UpdateAsync(Guid id, UpdateChannelConfigRequest req)
    {
        var entity = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found.");
        entity.ProviderName = req.ProviderName;
        entity.Label = req.Label;
        entity.LabelSlug = await ResolveSlugAsync(
            entity.ChannelType, req.LabelSlug, req.Label, entity.Id);
        entity.ConfigJson = enc.Encrypt(MergeConfigJson(entity.ConfigJson, req.ConfigJson));
        repo.Update(entity);
        await repo.SaveChangesAsync();
        return ToDto(entity);
    }

    /// <summary>
    /// Merges an incoming config payload over the stored one.
    /// Reads mask secret values as <see cref="MaskedValue"/>, so a client that saves back
    /// what it was shown would otherwise overwrite real credentials with the mask itself.
    /// A field sent as the mask keeps its stored value; a field omitted entirely is left
    /// untouched, which also gives partial updates for free.
    /// </summary>
    private string MergeConfigJson(string storedCipher, string incomingJson)
    {
        var incoming = JsonSerializer.Deserialize<Dictionary<string, string>>(incomingJson)
            ?? throw new InvalidOperationException("Channel configuration must be a JSON object.");

        Dictionary<string, string>? stored = null;
        try
        {
            stored = JsonSerializer.Deserialize<Dictionary<string, string>>(enc.Decrypt(storedCipher));
        }
        catch { /* unreadable stored config — handled below */ }

        if (stored is null)
        {
            // Nothing to merge against. Storing the mask verbatim would silently destroy
            // the credentials, so refuse rather than guess.
            if (incoming.Values.Any(v => v == MaskedValue))
                throw new InvalidOperationException(
                    "The saved configuration for this channel could not be read, so hidden fields "
                    + "cannot be preserved. Re-enter every credential field, then save again.");
            return incomingJson;
        }

        var merged = new Dictionary<string, string>(stored);
        foreach (var kv in incoming)
        {
            if (kv.Value == MaskedValue) continue;
            merged[kv.Key] = kv.Value;
        }
        return JsonSerializer.Serialize(merged);
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found.");
        // Guards the default, not merely the active flag: several configs of a type may
        // be active now, and those are legitimately deletable. Removing the default would
        // leave untagged sends of that type with nowhere to go.
        if (entity.IsDefault)
            throw new InvalidOperationException(
                "This channel is the default for its type. Make another channel the default first, then delete this one.");
        repo.Delete(entity);
        await repo.SaveChangesAsync();
    }

    /// <summary>Toggles availability. Independent of which config is the default.</summary>
    public async Task SetActiveAsync(Guid id, bool isActive)
    {
        var entity = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found.");

        if (!isActive && entity.IsDefault)
            throw new InvalidOperationException(
                "The default channel cannot be deactivated. Make another channel the default first.");

        entity.IsActive = isActive;
        repo.Update(entity);
        await repo.SaveChangesAsync();
    }

    public async Task SetDefaultAsync(Guid id)
    {
        var entity = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found.");
        await repo.SetDefaultAsync(id, entity.ChannelType);
    }

    /// <summary>
    /// Settles a config's slug: uses the supplied one when given, otherwise derives it
    /// from the label. Slugs must be unique per channel type, so collisions get a numeric
    /// suffix rather than failing the save.
    /// </summary>
    private async Task<string> ResolveSlugAsync(
        ChannelType type, string? requested, string label, Guid? excludeId)
    {
        var candidate = Slugify(string.IsNullOrWhiteSpace(requested) ? label : requested);
        if (candidate.Length == 0)
            throw new InvalidOperationException(
                "A channel label must contain at least one letter or digit so a tag slug can be derived from it.");

        var taken = (await repo.GetAllByChannelTypeAsync(type))
            .Where(c => c.Id != excludeId)
            .Select(c => c.LabelSlug)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!taken.Contains(candidate)) return candidate;

        for (var n = 2; ; n++)
        {
            var next = $"{candidate}{n}";
            if (!taken.Contains(next)) return next;
        }
    }

    /// <summary>
    /// Lowercase alphanumeric only. Hyphens are stripped rather than preserved because
    /// the combined tag form splits on "-wp_label:" — a hyphen inside a slug would make
    /// that ambiguous.
    /// </summary>
    private static string Slugify(string value) =>
        new(value.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());

    public async Task<string> GetDecryptedConfigJsonAsync(Guid id)
    {
        var entity = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found.");
        return enc.Decrypt(entity.ConfigJson);
    }

    private ChannelConfigDto ToDto(ChannelConfig c)
    {
        var masked = new Dictionary<string, string>();
        try
        {
            var plain = enc.Decrypt(c.ConfigJson);
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(plain);
            if (dict is not null)
                foreach (var kv in dict)
                    masked[kv.Key] = IsSecret(kv.Key) ? MaskedValue : kv.Value;
        }
        catch { masked["error"] = "unable_to_decode"; }

        return new ChannelConfigDto
        {
            Id = c.Id,
            ChannelType = c.ChannelType,
            ProviderName = c.ProviderName,
            Label = c.Label,
            LabelSlug = c.LabelSlug,
            IsActive = c.IsActive,
            IsDefault = c.IsDefault,
            ConfigFields = masked,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }

    /// <summary>Placeholder returned instead of a secret value; never persisted.</summary>
    private const string MaskedValue = "***";

    private static bool IsSecret(string key) =>
        key is "apikey" or "secretkey" or "access_token" or "password" or "authUserId";
}
