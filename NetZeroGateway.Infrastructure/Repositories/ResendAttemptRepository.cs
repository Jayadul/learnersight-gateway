using Microsoft.EntityFrameworkCore;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Infrastructure.Persistence;

namespace NetZeroGateway.Infrastructure.Repositories;

public class ResendAttemptRepository(NetZeroGatewayDbContext db)
    : Repository<ResendAttempt>(db), IResendAttemptRepository
{
    public Task<List<ResendAttempt>> GetByRecipientIdAsync(Guid dispatchJobRecipientId)
        => _set.Include(a => a.DispatchJob)
               .ThenInclude(j => j!.ChannelConfig)
               .Where(a => a.DispatchJobRecipientId == dispatchJobRecipientId)
               .OrderByDescending(a => a.AttemptedAt)
               .ToListAsync();
}
