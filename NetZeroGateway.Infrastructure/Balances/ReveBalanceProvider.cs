using Microsoft.Extensions.Logging;
using NetZeroGateway.Application.Strategies;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;

namespace NetZeroGateway.Infrastructure.Balances;

public class ReveBalanceProvider(
    IReveBalanceClient client,
    ILogger<ReveBalanceProvider> logger
) : IChannelBalanceProvider
{
    public Provider Provider => Provider.Reve;

    public async Task<ChannelBalanceResult> GetBalanceAsync(
        Dictionary<string, string> cfg, CancellationToken ct = default)
    {
        // Balance is opt-in per channel: a REVE channel configured only for sending has
        // no balance credentials, and that is not an error worth showing on a card.
        if (!cfg.TryGetValue("balanceClientId", out var clientId)
            || !cfg.TryGetValue("balanceSessionCookie", out var cookie)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(cookie))
        {
            return ChannelBalanceResult.Failed(
                "Balance is not configured for this channel. Add the Balance Client ID "
                + "and Balance Session Cookie in the channel settings.");
        }

        try
        {
            var r = await client.GetBalanceAsync(
                clientId, cookie, cfg.GetValueOrDefault("balanceBaseUrl"), ct);

            return r.IsSuccess
                ? ChannelBalanceResult.Ok(r.Balance!.Value, r.CreditLimit)
                : ChannelBalanceResult.Failed(r.Error ?? "REVE did not return a balance.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[REVE_BALANCE] Exception reading balance");
            return ChannelBalanceResult.Failed(ex.Message);
        }
    }
}
