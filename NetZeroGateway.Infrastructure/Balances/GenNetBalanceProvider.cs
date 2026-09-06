using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Balances;

public class GenNetBalanceProvider(
    IGenNetClient client,
    ILogger<GenNetBalanceProvider> logger
) : IChannelBalanceProvider
{
    public Provider Provider => Provider.GenNet;

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
                    ?? "GenNet did not return a balance.");
            }

            // GenNet has no credit-limit concept — null, not zero.
            return ChannelBalanceResult.Ok(balance.Value);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[GENNET] Exception reading balance");
            return ChannelBalanceResult.Failed(ex.Message);
        }
    }
}
