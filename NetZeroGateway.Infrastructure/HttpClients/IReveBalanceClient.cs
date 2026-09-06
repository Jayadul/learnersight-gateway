using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Infrastructure.HttpClients;

public interface IReveBalanceClient
{
    /// <summary>
    /// Reads REVE's client balance. This lives on a different host and scheme from the
    /// send endpoint (https, no port), so it takes its own base URL rather than reusing
    /// the channel's send <c>baseUrl</c>.
    /// <para>
    /// Authenticated by a <c>JSESSIONID</c> cookie. REVE has given us no login or
    /// refresh endpoint, so when the session expires the call fails and an admin pastes
    /// a fresh cookie into the channel — there is deliberately no retry-login loop.
    /// </para>
    /// </summary>
    Task<ReveBalanceResponse> GetBalanceAsync(
        string clientId, string sessionCookie,
        string? baseUrl = null,
        CancellationToken ct = default);
}
