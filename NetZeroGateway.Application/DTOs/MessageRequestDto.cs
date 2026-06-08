using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class MessageRequestDto
{
    public Guid           Id             { get; set; }
    public string         ToUser         { get; set; } = string.Empty;
    public List<string>   ParsedChannels { get; set; } = [];
    public MessageStatus  OverallStatus  { get; set; }
    public DateTimeOffset ReceivedAt     { get; set; }
    public MessageSource  Source         { get; set; }
    public DateTimeOffset CreatedAt      { get; set; }
}
