using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IResendService
{
    /// <summary>
    /// Retries the named recipients, optionally through another channel of the same
    /// type. Every attempt creates a new job and new recipient rows linked back to the
    /// originals — nothing is mutated in place, so the original failure stays visible.
    /// </summary>
    Task<ResendResultDto> ResendAsync(ResendRequest request);
}
