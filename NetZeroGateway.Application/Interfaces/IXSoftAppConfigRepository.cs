using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Interfaces;

public interface IXSoftAppConfigRepository : IRepository<XSoftAppConfig>
{
    Task<XSoftAppConfig?> GetActiveAsync();
}
