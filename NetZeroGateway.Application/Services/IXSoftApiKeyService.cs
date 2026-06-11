using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface IXSoftApiKeyService
{
    Task<GenerateApiKeyResponse> GenerateAsync(string label);
    Task<bool> ValidateAsync(string apiKey, string secretKey);
    Task<List<XSoftApiKeyDto>> GetAllAsync();
    Task<bool> RevokeAsync(Guid id);
}
