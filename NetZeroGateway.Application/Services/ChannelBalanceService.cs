using System.Text.Json;
using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Services;

/// <summary>
/// Keeps each channel's provider balance cached on the channel row, and refreshes it on
/// demand or on a schedule. <see cref="GetAllAsync"/> never calls a provider — the
/// dashboard shows several channels at once, and one slow vendor would otherwise hold up
/// the whole page — but <see cref="GetByLabelAsync"/> (the public, unauthenticated
/// endpoint) does call live on every request; see its own doc comment.
/// </summary>
public class ChannelBalanceService(
    IChannelConfigRepository repo,
    IEncryptionService enc,
    IEnumerable<IChannelBalanceProvider> providers,
    ILogger<ChannelBalanceService> logger
) : IChannelBalanceService
{
    private readonly Dictionary<Domain.Enums.Provider, IChannelBalanceProvider> _providers =
        providers.ToDictionary(p => p.Provider);

    public async Task<List<ChannelBalanceDto>> GetAllAsync()
    {
        var configs = await repo.GetAllAsync();
        return configs
            .Where(c => _providers.ContainsKey(c.Provider))
            .OrderBy(c => c.ChannelType)
            .ThenBy(c => c.Label)
            .Select(ToDto)
            .ToList();
    }

    public async Task<ChannelBalanceDto> RefreshAsync(Guid channelConfigId)
    {
        var config = await repo.GetByIdAsync(channelConfigId)
            ?? throw new NotFoundException($"ChannelConfig {channelConfigId} not found.");

        if (!_providers.ContainsKey(config.Provider))
        {
            throw new InvalidOperationException(
                $"Provider '{config.Provider}' does not report a balance. "
                + "Balance is available for REVE and Chithi channels.");
        }

        await RefreshOneAsync(config);
        await repo.SaveChangesAsync();
        return ToDto(config);
    }

    public async Task RefreshAllAsync()
    {
        var configs = (await repo.GetAllAsync())
            .Where(c => c.IsActive && _providers.ContainsKey(c.Provider))
            .ToList();

        logger.LogInformation("[BALANCE] Refresh starting for {Count} channels", configs.Count);

        foreach (var config in configs)
        {
            // Per-channel try/catch: one unreachable provider must not stop the batch.
            try
            {
                await RefreshOneAsync(config);
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex, "[BALANCE] Refresh failed for {Label}", config.Label);
            }
        }

        await repo.SaveChangesAsync();
        logger.LogInformation("[BALANCE] Refresh complete");
    }

    /// <summary>
    /// Reads one channel's balance and records the outcome. A failure keeps the last
    /// known figure and its timestamp, and only sets the error — blanking a balance
    /// because the provider was briefly down would read as "no credit left".
    /// </summary>
    private async Task RefreshOneAsync(ChannelConfig config)
    {
        var cfg = JsonSerializer.Deserialize<Dictionary<string, string>>(
            enc.Decrypt(config.ConfigJson)) ?? [];

        var result = await _providers[config.Provider].GetBalanceAsync(cfg);

        if (result.Success)
        {
            config.Balance = result.Available;
            config.BalanceCreditLimit = result.CreditLimit;
            config.BalanceCheckedAt = DateTimeOffset.UtcNow;
            config.BalanceError = null;
            logger.LogInformation(
                "[BALANCE] {Label}: {Balance}", config.Label, result.Available);
        }
        else
        {
            config.BalanceError = result.Error;
            logger.LogWarning(
                "[BALANCE] {Label} failed: {Error}", config.Label, result.Error);
        }

        repo.Update(config);
    }

    /// <summary>
    /// Team-lead-approved reversal of the original R3.3 design (this used to read the
    /// cache only, like <see cref="GetAllAsync"/>): the public endpoint now calls the
    /// provider live on every request. Flagged once, for the record — this endpoint has
    /// no authentication, so a label is now enough to trigger unlimited live REVE/Chithi
    /// balance calls; there is no rate limiting here.
    /// <para>
    /// A failed live read still returns the last known cached figure — the same
    /// graceful-degradation rule <see cref="RefreshOneAsync"/> already applies
    /// everywhere else, so a transient provider hiccup does not turn into a bare error
    /// for an integrator polling this endpoint.
    /// </para>
    /// </summary>
    public async Task<PublicBalanceDto?> GetByLabelAsync(string label)
    {
        var config = (await repo.GetAllAsync()).FirstOrDefault(c =>
            c.IsActive && string.Equals(c.LabelSlug, label, StringComparison.OrdinalIgnoreCase));

        if (config is null || !_providers.ContainsKey(config.Provider)) return null;

        try
        {
            await RefreshOneAsync(config);
            await repo.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[BALANCE] Live read failed for label {Label}", label);
        }

        return new PublicBalanceDto
        {
            Label = config.Label,
            Balance = config.Balance,
            CreditLimit = config.BalanceCreditLimit,
            LastCheckedAt = config.BalanceCheckedAt
        };
    }

    private static ChannelBalanceDto ToDto(ChannelConfig c) => new()
    {
        ChannelConfigId = c.Id,
        Label = c.Label,
        ChannelType = c.ChannelType,
        Provider = c.Provider,
        ProviderName = c.ProviderName,
        IsActive = c.IsActive,
        Available = c.Balance,
        CreditLimit = c.BalanceCreditLimit,
        CheckedAt = c.BalanceCheckedAt,
        Error = c.BalanceError
    };
}
