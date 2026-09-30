using System.Linq.Expressions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Hotfix batch 9, Task 1: a Template channel can now register its Meta-approved
/// template names (and reference variable labels) instead of the old free-text-only
/// field with zero persistence.
/// </summary>
public class ChannelTemplateServiceTests
{
    private sealed class SingleChannelRepository(ChannelConfig channel) : IChannelConfigRepository
    {
        public Task<ChannelConfig?> GetByIdAsync(Guid id)
            => Task.FromResult(id == channel.Id ? channel : null);
        public Task<ChannelConfig?> GetDefaultByChannelTypeAsync(ChannelType type) => Unused.Fail<Task<ChannelConfig?>>();
        public Task<ChannelConfig?> GetActiveBySlugAsync(ChannelType type, string labelSlug) => Unused.Fail<Task<ChannelConfig?>>();
        public Task<List<ChannelConfig>> GetAllByChannelTypeAsync(ChannelType type) => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task SetDefaultAsync(Guid id, ChannelType type) => Unused.Fail<Task>();
        public Task<List<ChannelConfig>> GetAllAsync() => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task<List<ChannelConfig>> FindAsync(Expression<Func<ChannelConfig, bool>> p) => Unused.Fail<Task<List<ChannelConfig>>>();
        public Task AddAsync(ChannelConfig entity) => Unused.Fail<Task>();
        public void Update(ChannelConfig entity) { }
        public void Delete(ChannelConfig entity) => Unused.Fail<bool>();
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    /// <summary>An in-memory, mutable stand-in — Add/Update/Delete actually mutate the list.</summary>
    private sealed class MutableTemplateRepository : IChannelTemplateRepository
    {
        public readonly List<ChannelTemplate> Store = [];
        public Task<List<ChannelTemplate>> GetAllByChannelAsync(Guid channelConfigId)
            => Task.FromResult(Store.Where(t => t.ChannelConfigId == channelConfigId).ToList());
        public Task<ChannelTemplate?> GetByChannelAndNameAsync(Guid channelConfigId, string name)
            => Task.FromResult(Store.FirstOrDefault(t =>
                t.ChannelConfigId == channelConfigId && t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
        public Task<ChannelTemplate?> GetByIdAsync(Guid id)
            => Task.FromResult(Store.FirstOrDefault(t => t.Id == id));
        public Task<List<ChannelTemplate>> GetAllAsync() => Task.FromResult(Store.ToList());
        public Task<List<ChannelTemplate>> FindAsync(Expression<Func<ChannelTemplate, bool>> p) => Unused.Fail<Task<List<ChannelTemplate>>>();
        public Task AddAsync(ChannelTemplate entity) { Store.Add(entity); return Task.CompletedTask; }
        public void Update(ChannelTemplate entity) { }
        public void Delete(ChannelTemplate entity) => Store.RemoveAll(t => t.Id == entity.Id);
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
    }

    private static ChannelConfig TemplateChannel() => new()
    {
        Id = Guid.NewGuid(),
        ChannelType = ChannelType.WP,
        Provider = Provider.NetZeroInboxTemplate,
        ProviderName = "NetZero Inbox Template",
        Label = "WA Template",
        LabelSlug = "watemplate",
    };

    private static ChannelConfig OrdinaryChannel() => new()
    {
        Id = Guid.NewGuid(),
        ChannelType = ChannelType.WP,
        Provider = Provider.NetZeroInbox,
        ProviderName = "NetZero Inbox",
        Label = "WA Primary",
        LabelSlug = "waprimary",
    };

    [Fact]
    public async Task Cannot_register_a_template_on_a_non_template_channel()
    {
        var channel = OrdinaryChannel();
        var service = new ChannelTemplateService(new MutableTemplateRepository(), new SingleChannelRepository(channel));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(channel.Id, new CreateChannelTemplateRequest { Name = "order_confirmation" }));
    }

    [Fact]
    public async Task Create_trims_blank_variable_labels_and_derives_the_count()
    {
        var channel = TemplateChannel();
        var service = new ChannelTemplateService(new MutableTemplateRepository(), new SingleChannelRepository(channel));

        var dto = await service.CreateAsync(channel.Id, new CreateChannelTemplateRequest
        {
            Name = "order_confirmation",
            VariableLabels = ["Customer Name", "  ", "Order ID", ""]
        });

        Assert.Equal(["Customer Name", "Order ID"], dto.VariableLabels);
        Assert.Equal(2, dto.VariableCount);
    }

    [Fact]
    public async Task Duplicate_name_on_the_same_channel_is_rejected_case_insensitively()
    {
        var channel = TemplateChannel();
        var service = new ChannelTemplateService(new MutableTemplateRepository(), new SingleChannelRepository(channel));

        await service.CreateAsync(channel.Id, new CreateChannelTemplateRequest { Name = "order_confirmation" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(channel.Id, new CreateChannelTemplateRequest { Name = "ORDER_CONFIRMATION" }));
    }

    [Fact]
    public async Task Delete_removes_it_from_the_channels_list()
    {
        var channel = TemplateChannel();
        var repo = new MutableTemplateRepository();
        var service = new ChannelTemplateService(repo, new SingleChannelRepository(channel));

        var dto = await service.CreateAsync(channel.Id, new CreateChannelTemplateRequest { Name = "order_confirmation" });
        await service.DeleteAsync(channel.Id, dto.Id);

        Assert.Empty(await service.GetAllAsync(channel.Id));
    }

    [Fact]
    public async Task Rename_to_a_different_existing_templates_name_is_rejected()
    {
        var channel = TemplateChannel();
        var service = new ChannelTemplateService(new MutableTemplateRepository(), new SingleChannelRepository(channel));

        await service.CreateAsync(channel.Id, new CreateChannelTemplateRequest { Name = "order_confirmation" });
        var second = await service.CreateAsync(channel.Id, new CreateChannelTemplateRequest { Name = "shipping_update" });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateAsync(channel.Id, second.Id, new UpdateChannelTemplateRequest { Name = "order_confirmation" }));
    }
}
