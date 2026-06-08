using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using System.Text.Json;

namespace NetZeroGateway.Application.Services;

/// <summary>
/// Uses IVoiceReportGateway (Application interface) so the service
/// never touches Infrastructure types directly.
/// </summary>
public class VoiceReportService(
    IChannelConfigRepository configRepo,
    IEncryptionService       enc,
    IVoiceReportGateway      reportGateway
) : IVoiceReportService
{
    public async Task<VoiceReportSyncResponse> SyncAsync(VoiceReportSyncRequest req)
    {
        var config = await configRepo.GetActiveByChannelTypeAsync(Domain.Enums.ChannelType.Voice)
            ?? throw new NotFoundException("No active Voice channel config found.");

        var plain = enc.Decrypt(config.ConfigJson);
        var cfg   = JsonSerializer.Deserialize<Dictionary<string, string>>(plain)
            ?? throw new InvalidOperationException("Invalid voice config JSON.");

        var owner    = cfg.GetValueOrDefault("owner",    "");
        var password = cfg.GetValueOrDefault("password", "");

        var result = await reportGateway.GetCombinedReportAsync(owner, password, req.From, req.To);

        return new VoiceReportSyncResponse
        {
            TotalRecords = result.TotalRecords,
            Records      = result.Records
        };
    }
}
