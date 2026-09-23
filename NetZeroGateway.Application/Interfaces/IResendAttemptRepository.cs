using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.Interfaces;

public interface IResendAttemptRepository : IRepository<ResendAttempt>
{
    /// <summary>A recipient's own resend history, most recent first.</summary>
    Task<List<ResendAttempt>> GetByRecipientIdAsync(Guid dispatchJobRecipientId);
}
