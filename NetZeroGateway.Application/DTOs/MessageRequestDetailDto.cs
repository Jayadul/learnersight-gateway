using NetZeroGateway.Domain.Enums;
namespace NetZeroGateway.Application.DTOs;
public class MessageRequestDetailDto : MessageRequestDto
{
    public List<DispatchJobDto> Jobs { get; set; } = [];
}
