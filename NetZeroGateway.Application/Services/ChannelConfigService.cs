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
    IChannelTemplateRepository templateRepo,
    IDispatchJobRepository jobRepo,
    IEncryptionService enc,
    IGatewayCredentialService gatewayCredentials
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
            IsDefault = siblings.Count == 0 && req.Provider != Provider.NetZeroInboxTemplate
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
        var balanceClientIdBefore = BalanceClientIdOf(entity);

        entity.Provider = ValidateProvider(entity.ChannelType, req.Provider);
        entity.ProviderName = req.ProviderName;
        entity.Label = req.Label;
        entity.LabelSlug = await ResolveSlugAsync(
            entity.ChannelType, req.LabelSlug, req.Label, entity.Id);
        entity.DelaySeconds = ValidateDelay(req.DelaySeconds);
        entity.ConfigJson = enc.Encrypt(MergeConfigJson(entity.ConfigJson, req.ConfigJson));
        repo.Update(entity);
        await repo.SaveChangesAsync();

        // The gateway's Username tracks the default SMS channel's own Balance Client
        // ID — only relevant when this is that channel, and only worth a write when the
        // value actually moved.
        if (entity.IsDefault && entity.ChannelType == ChannelType.SMS)
        {
            var balanceClientIdAfter = BalanceClientIdOf(entity);
            if (balanceClientIdAfter != balanceClientIdBefore)
                await gatewayCredentials.SyncUsernameFromDefaultSmsChannelAsync(balanceClientIdAfter);
        }

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
            if (IsMaskedPlaceholder(kv.Value)) continue;
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

        // The dispatch_jobs FK is Restrict too (hotfix batch 9, Task 16) — a channel that
        // has ever sent anything would otherwise fail this with a raw DB constraint
        // violation instead of a message the admin can act on. Deactivating keeps the
        // channel out of rotation without losing the audit trail a hard delete would.
        if (await jobRepo.ExistsForChannelAsync(id))
            throw new InvalidOperationException(
                $"'{entity.Label}' has sent message(s) and cannot be deleted, to keep that "
                + "history intact. Deactivate it instead to stop it from being used.");

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
        // Untagged and fallback sends of a type go to its default, and a template channel
        // cannot carry those.
        if (entity.Provider == Provider.NetZeroInboxTemplate)
            throw new InvalidOperationException(
                "A WhatsApp Template channel cannot be the default: it only sends templates.");
        await repo.SetDefaultAsync(id, entity.ChannelType);

        if (entity.ChannelType == ChannelType.SMS)
            await gatewayCredentials.SyncUsernameFromDefaultSmsChannelAsync(BalanceClientIdOf(entity));
    }

    /// <summary>
    /// The channel's own Balance Client ID, or null when it has none — only REVE-style
    /// configs carry this field, and an undecryptable config must not throw here.
    /// </summary>
    private string? BalanceClientIdOf(ChannelConfig c)
    {
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(enc.Decrypt(c.ConfigJson));
            return dict?.GetValueOrDefault("balanceClientId");
        }
        catch { return null; }
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

    public async Task<string> RevealFieldAsync(Guid id, string field)
    {
        var entity = await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"ChannelConfig {id} not found.");

        Dictionary<string, string>? dict;
        try
        {
            dict = JsonSerializer.Deserialize<Dictionary<string, string>>(enc.Decrypt(entity.ConfigJson));
        }
        catch
        {
            throw new InvalidOperationException(
                "This channel's configuration could not be decrypted.");
        }

        if (dict is null || !dict.TryGetValue(field, out var value))
            throw new NotFoundException($"Field '{field}' is not set on this channel.");

        return value;
    }

    public async Task<List<ChannelOptionDto>> GetOptionsAsync(ChannelType? filter)
    {
        var list = filter is null
            ? await repo.GetAllAsync()
            : await repo.GetAllByChannelTypeAsync(filter.Value);

        var result = new List<ChannelOptionDto>();
        foreach (var c in list)
        {
            var isTemplateOnly = c.Provider == Provider.NetZeroInboxTemplate;
            result.Add(new ChannelOptionDto
            {
                Id = c.Id,
                Label = c.Label,
                LabelSlug = c.LabelSlug,
                ChannelType = c.ChannelType,
                IsActive = c.IsActive,
                IsDefault = c.IsDefault,
                // Only a dedicated Template channel counts: a templateToken left on a
                // regular channel from before batch 6 is no longer offered for template sends.
                HasTemplateToken = isTemplateOnly && HasTemplateToken(c),
                IsTemplateOnly = isTemplateOnly,
                // Registered templates (Task 1) feed Manual Send's dropdown — empty for
                // every non-Template channel, so this stays a single extra query only
                // when it can possibly matter.
                Templates = isTemplateOnly
                    ? (await templateRepo.GetAllByChannelAsync(c.Id)).Select(t => new ChannelTemplateDto
                    {
                        Id = t.Id,
                        ChannelConfigId = t.ChannelConfigId,
                        Name = t.Name,
                        VariableLabels = t.VariableLabels,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt
                    }).ToList()
                    : []
            });
        }
        return result;
    }

    /// <summary>
    /// Whether this channel has a usable Template API Token — decrypted just to check
    /// presence, never returned by this endpoint (which stays credential-free for its
    /// non-SuperAdmin callers). An undecryptable config just reads as "no token" rather
    /// than throwing, since this list must keep rendering even if one channel's config
    /// is unreadable.
    /// </summary>
    private bool HasTemplateToken(ChannelConfig c)
    {
        try
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(enc.Decrypt(c.ConfigJson));
            return !string.IsNullOrWhiteSpace(dict?.GetValueOrDefault("templateToken"));
        }
        catch { return false; }
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
    /// providers that have a registered sender, not every member of the enum — allowing
    /// one to be saved before its sender exists would create a channel that looks
    /// configured and throws on the first real message. Add each here as its sender is
    /// registered. See ADR 0008.
    /// </summary>
    private static Provider[] ProvidersFor(ChannelType channelType) => channelType switch
    {
        ChannelType.SMS => [Provider.Reve, Provider.Chithi],
        ChannelType.WP => [Provider.WaTrend, Provider.NetZeroInbox, Provider.NetZeroInboxQr, Provider.NetZeroInboxTemplate],
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
                    masked[kv.Key] = IsSecret(kv.Key) ? PartialMask(kv.Value) : kv.Value;
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

    /// <summary>
    /// The mask prefix — plain ASCII, deliberately, after finding during testing that a
    /// Unicode bullet character does not survive every request path unmangled (a
    /// PowerShell/Invoke-RestMethod round trip silently corrupted it), which would have
    /// broken the "is this still masked" check below and let a mangled placeholder
    /// overwrite a real stored secret. "***" is also the exact legacy full-mask value, so
    /// old and new masked reads are recognised by the same prefix check.
    /// </summary>
    private const string MaskedValue = "***";

    /// <summary>
    /// Partial mask (hotfix batch 3): keeps the last few characters visible so an admin
    /// can recognise which credential a field holds at a glance, e.g. "***7ced465" — full
    /// reveal is a separate, explicit action (<see cref="RevealFieldAsync"/>), not the
    /// default read.
    /// </summary>
    private static string PartialMask(string value)
    {
        const int tailLength = 6;
        // A short value shows no characters at all rather than revealing all of it — the
        // same "***" a fully-masked field has always shown.
        var tail = value.Length > tailLength ? value[^tailLength..] : "";
        return MaskedValue + tail;
    }

    /// <summary>
    /// Whether an incoming config value is a masked placeholder rather than a real,
    /// intentionally-entered one — the signal MergeConfigJson uses to keep the stored
    /// value instead of overwriting it with the mask itself. Every masked read, full or
    /// partial, starts with <see cref="MaskedValue"/>; a real credential starting with
    /// three literal asterisks is not a realistic case for any of the providers this
    /// gateway integrates with.
    /// </summary>
    private static bool IsMaskedPlaceholder(string value) =>
        value.StartsWith(MaskedValue, StringComparison.Ordinal);

    /// <summary>
    /// Whether a config field is a credential and must be masked on read.
    /// <para>
    /// This used to be an exact list of the five field names REVE, WaTrend and Voice
    /// happened to use, which meant every provider added afterwards leaked its
    /// credential through the admin API until someone remembered to extend the list.
    /// Matching on the shape of the name makes the safe behaviour the default: a new
    /// provider's <c>api_token</c> or session cookie is masked without anyone
    /// remembering anything.
    /// </para>
    /// <para>
    /// Deliberately not masked: <c>baseUrl</c>, <c>callerID</c>, <c>instance_id</c>,
    /// <c>sid</c>, <c>balanceClientId</c>, <c>campaignId</c>, <c>lotNumber</c> and
    /// <c>owner</c> — identifiers an operator needs to read to tell two channels apart.
    /// <c>authUserId</c> stays listed by name because it carries a credential despite
    /// reading like an identifier.
    /// </para>
    /// </summary>
    private static bool IsSecret(string key)
    {
        if (key.Equals("authUserId", StringComparison.OrdinalIgnoreCase)) return true;

        return SecretMarkers.Any(marker =>
            key.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Applies a seeded config to an existing channel's stored one. Only credential fields
    /// (and fields the channel does not have yet) come from the seed; everything else stays
    /// as the admin left it, including fields the seed never carried. Replacing the whole
    /// config reverted every admin edit, and dropped added fields, on each restart.
    /// A stored config that cannot be read is replaced by the seed outright.
    /// </summary>
    public static string MergeSeededConfig(string? existingPlain, IReadOnlyDictionary<string, string> seed)
    {
        Dictionary<string, string>? merged = null;
        try { merged = JsonSerializer.Deserialize<Dictionary<string, string>>(existingPlain ?? ""); }
        catch (JsonException) { }

        if (merged is null) return JsonSerializer.Serialize(seed);

        foreach (var (key, value) in seed)
        {
            if (IsSecret(key) || !merged.ContainsKey(key))
                merged[key] = value;
        }
        return JsonSerializer.Serialize(merged);
    }

    private static readonly string[] SecretMarkers =
        ["secret", "token", "password", "cookie", "apikey"];
}
