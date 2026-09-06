using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Balances;

public class ChithiBalanceProvider(
    IChithiClient client,
    ILogger<ChithiBalanceProvider> logger
) : IChannelBalanceProvider
{
    public Provider Provider => Provider.Chithi;

    public async Task<ChannelBalanceResult> GetBalanceAsync(
        Dictionary<string, string> cfg, CancellationToken ct = default)
    {
        if (!cfg.TryGetValue("api_token", out var token) || string.IsNullOrWhiteSpace(token))
            return ChannelBalanceResult.Failed("This channel has no API token configured.");

        try
        {
            var r = await client.GetBalanceAsync(token, ProviderEndpoint.BaseUrlOf(cfg), ct);

            var balance = r.SmsInfo?.BalanceInfo?.FirstOrDefault()?.SmsBalance;
            if (!r.IsSuccess || balance is null)
            {
                return ChannelBalanceResult.Failed(
                    r.Description ?? r.StatusMeaning ?? r.ErrorMessage ?? r.Status
                    ?? "Chithi did not return a balance.");
            }

            // As with GenNet, no credit-limit concept.
            return ChannelBalanceResult.Ok(balance.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[CHITHI] Exception reading balance");
            return ChannelBalanceResult.Failed(ex.Message);
        }
    }
}
