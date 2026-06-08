namespace NetZeroGateway.Application.Services;

public interface IFallbackHandlerService
{
    Task HandleAsync(Guid messageRequestId);
}
