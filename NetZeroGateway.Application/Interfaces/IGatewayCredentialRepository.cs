using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Interfaces;

public interface IGatewayCredentialRepository : IRepository<GatewayCredential>
{
    /// <summary>
    /// The gateway's one credential row, or null before it has been seeded. Ordered by
    /// creation so a database that somehow holds two rows still resolves the same way on
    /// every request rather than alternating.
    /// </summary>
    Task<GatewayCredential?> GetCurrentAsync();
}
