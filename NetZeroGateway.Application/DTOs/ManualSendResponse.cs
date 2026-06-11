namespace NetZeroGateway.Application.DTOs;
public class ManualSendResponse
{
    public Guid MessageRequestId { get; set; }
    public string Status { get; set; } = "queued";
    public int TotalNumbers { get; set; }
}
