using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Exceptions;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public class ChannelTemplateService(
    IChannelTemplateRepository repo,
    IChannelConfigRepository channelRepo
) : IChannelTemplateService
{
    public async Task<List<ChannelTemplateDto>> GetAllAsync(Guid channelConfigId)
    {
        await RequireTemplateChannelAsync(channelConfigId);
        return (await repo.GetAllByChannelAsync(channelConfigId)).Select(ToDto).ToList();
    }

    public async Task<ChannelTemplateDto> CreateAsync(Guid channelConfigId, CreateChannelTemplateRequest req)
    {
        await RequireTemplateChannelAsync(channelConfigId);
        var name = RequireName(req.Name);

        if (await repo.GetByChannelAndNameAsync(channelConfigId, name) is not null)
            throw new InvalidOperationException(
                $"A template named '{name}' is already registered on this channel.");

        var entity = new ChannelTemplate
        {
            ChannelConfigId = channelConfigId,
            Name = name,
            VariableLabels = CleanLabels(req.VariableLabels)
        };
        await repo.AddAsync(entity);
        await repo.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task<ChannelTemplateDto> UpdateAsync(Guid channelConfigId, Guid templateId, UpdateChannelTemplateRequest req)
    {
        await RequireTemplateChannelAsync(channelConfigId);
        var entity = await repo.GetByIdAsync(templateId);
        if (entity is null || entity.ChannelConfigId != channelConfigId)
            throw new NotFoundException($"Template {templateId} not found on this channel.");

        var name = RequireName(req.Name);
        var existing = await repo.GetByChannelAndNameAsync(channelConfigId, name);
        if (existing is not null && existing.Id != templateId)
            throw new InvalidOperationException(
                $"A template named '{name}' is already registered on this channel.");

        entity.Name = name;
        entity.VariableLabels = CleanLabels(req.VariableLabels);
        repo.Update(entity);
        await repo.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task DeleteAsync(Guid channelConfigId, Guid templateId)
    {
        await RequireTemplateChannelAsync(channelConfigId);
        var entity = await repo.GetByIdAsync(templateId);
        if (entity is null || entity.ChannelConfigId != channelConfigId)
            throw new NotFoundException($"Template {templateId} not found on this channel.");

        repo.Delete(entity);
        await repo.SaveChangesAsync();
    }

    /// <summary>Templates only make sense on a dedicated Template channel — the config that would actually send them.</summary>
    private async Task RequireTemplateChannelAsync(Guid channelConfigId)
    {
        var channel = await channelRepo.GetByIdAsync(channelConfigId)
            ?? throw new NotFoundException($"ChannelConfig {channelConfigId} not found.");
        if (channel.Provider != Provider.NetZeroInboxTemplate)
            throw new InvalidOperationException(
                "Templates can only be registered on a WhatsApp Template channel.");
    }

    private static string RequireName(string name)
    {
        var trimmed = (name ?? "").Trim();
        if (trimmed.Length == 0)
            throw new InvalidOperationException(
                "Enter the template name exactly as approved by Meta.");
        return trimmed;
    }

    /// <summary>Blank labels are dropped rather than stored as empty strings — a gap in the list would misstate the variable count.</summary>
    private static List<string> CleanLabels(List<string>? labels) =>
        (labels ?? []).Select(l => l.Trim()).Where(l => l.Length > 0).ToList();

    private static ChannelTemplateDto ToDto(ChannelTemplate t) => new()
    {
        Id = t.Id,
        ChannelConfigId = t.ChannelConfigId,
        Name = t.Name,
        VariableLabels = t.VariableLabels,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}
