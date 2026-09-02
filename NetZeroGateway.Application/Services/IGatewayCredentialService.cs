using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IGatewayCredentialService
{
    /// <summary>The current credentials with both keys masked.</summary>
    Task<GatewayCredentialDto> GetAsync();

    /// <summary>Applies only the fields that were actually supplied.</summary>
    Task<GatewayCredentialDto> UpdateAsync(UpdateGatewayCredentialRequest request);
}
