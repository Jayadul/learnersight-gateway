using NetZeroGateway.Application.DTOs;

namespace NetZeroGateway.Application.Services;

public interface ITemplateParserService
{
    ParsedTemplate Parse(string messageContent);
}
