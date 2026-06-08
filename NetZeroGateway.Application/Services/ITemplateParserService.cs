using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface ITemplateParserService
{
    List<ParsedChannelJob> Parse(string messageContent);
}
