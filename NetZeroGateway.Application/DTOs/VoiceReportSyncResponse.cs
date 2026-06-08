using NetZeroGateway.Domain.Entities;

namespace NetZeroGateway.Application.DTOs;

public class VoiceReportSyncResponse
{
    public int                   TotalRecords { get; set; }
    public List<VoiceCallRecord> Records      { get; set; } = [];
}
