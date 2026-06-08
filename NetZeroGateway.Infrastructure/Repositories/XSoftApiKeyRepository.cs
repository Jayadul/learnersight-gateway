using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class XSoftApiKeyRepository(NetZeroGatewayDbContext db)
    : Repository<XSoftApiKey>(db), IXSoftApiKeyRepository { }
