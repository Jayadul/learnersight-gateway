using System.Text.Json;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class ChannelConfigService(
    IChannelConfigRepository repo,
    IFallbackPolicyRepository policyRepo,
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
            Provider = ValidateProvider(req.ChannelType, req.Provider),
            ProviderName = req.ProviderName,
            Label = req.Label,
            LabelSlug = await ResolveSlugAsync(req.ChannelType, req.LabelSlug, req.Label, null),
            DelaySeconds = ValidateDelay(req.DelaySeconds),
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
        entity.Provider = ValidateProvider(entity.ChannelType, req.Provider);
        entity.ProviderName = req.ProviderName;
        entity.Label = req.Label;
        entity.LabelSlug = await ResolveSlugAsync(
            entity.ChannelType, req.LabelSlug, req.Label, entity.Id);
        entity.DelaySeconds = ValidateDelay(req.DelaySeconds);
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

        // The fallback FKs are Restrict, so the database would refuse this with a raw
        // constraint violation. Name the rules instead so the admin knows what to edit.
        var referencing = await policyRepo.GetNamesReferencingChannelAsync(id);
        if (referencing.Count > 0)
            throw new InvalidOperationException(
                $"This channel is used by fallback rule(s): {string.Join(", ", referencing)}. "
                + "Update or remove them first, then delete this channel.");

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
    /// Bounded so a mistyped value cannot stall dispatch. The delay applies to every
    /// attempt, so a large recipient list multiplies it — 300s per send is already far
    /// beyond any sane pacing need.
    /// </summary>
    private static int ValidateDelay(int seconds)
    {
        const int max = 300;
        if (seconds < 0 || seconds > max)
            throw new InvalidOperationException(
                $"Send delay must be between 0 and {max} seconds.");
        return seconds;
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

    /// <summary>
    /// Rejects a provider that cannot serve this channel type. Saving a Voice channel
    /// with the WaTrend provider would otherwise persist happily and then throw at
    /// dispatch, when there is a real message to lose — better to refuse the save.
    /// </summary>
    private static Provider ValidateProvider(ChannelType channelType, Provider provider)
    {
        var allowed = ProvidersFor(channelType);
        if (allowed.Contains(provider)) return provider;

        throw new InvalidOperationException(
            $"Provider '{provider}' does not serve {channelType} channels. "
            + $"Valid providers for {channelType}: {string.Join(", ", allowed)}.");
    }

    /// <summary>
    /// Which vendors an operator may currently select for a channel type. This lists
    /// providers that have a registered sender, not every member of the enum — GenNet,
    /// Chithi and NetZeroInbox exist in <see cref="Provider"/> and land in R3.1, and
    /// allowing one to be saved before its sender exists would create a channel that
    /// looks configured and throws on the first real message. Add each here as its
    /// sender is registered. See ADR 0008.
    /// </summary>
    private static Provider[] ProvidersFor(ChannelType channelType) => channelType switch
    {
        ChannelType.SMS => [Provider.Reve],
        ChannelType.WP => [Provider.WaTrend],
        ChannelType.Voice => [Provider.NetZeroVoice],
        _ => []
    };

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
            Provider = c.Provider,
            ProviderName = c.ProviderName,
            Label = c.Label,
            LabelSlug = c.LabelSlug,
            IsActive = c.IsActive,
            IsDefault = c.IsDefault,
            DelaySeconds = c.DelaySeconds,
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
