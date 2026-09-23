using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// A manual send with nothing to send, or nobody to send it to, used to return 202 and
/// then resolve to Failed. That reads in the delivery log as a provider problem rather
/// than a malformed request, and left a row that never had anywhere to go.
/// </summary>
public class ManualSendValidationTests
{
    private sealed class NoWriteMessageRepo : IMessageRequestRepository
    {
        public List<MessageRequest> Added { get; } = [];
        public Task AddAsync(MessageRequest e) { Added.Add(e); return Task.CompletedTask; }
        public Task<int> SaveChangesAsync() => Task.FromResult(0);
        public void Update(MessageRequest e) { }
        public Task<MessageRequest?> GetWithJobsAsync(Guid id) => Task.FromResult<MessageRequest?>(null);
        public Task<MessageRequest?> GetByIdAsync(Guid id) => Unused.Fail<Task<MessageRequest?>>();
        public Task<List<MessageRequest>> GetAllAsync() => Unused.Fail<Task<List<MessageRequest>>>();
        public Task<List<MessageRequest>> FindAsync(Expression<Func<MessageRequest, bool>> p)
            => Unused.Fail<Task<List<MessageRequest>>>();
        public void Delete(MessageRequest e) => Unused.Fail<bool>();
        public Task<(List<MessageRequest> Items, int Total)> GetPagedAsync(
            int page, int size, DateTimeOffset? from, DateTimeOffset? to,
            string? toUser, MessageStatus? status, MessageSource? source, Guid? channelConfigId)
            => Unused.Fail<Task<(List<MessageRequest>, int)>>();
    }

    private static (ManualSendService Service, NoWriteMessageRepo Repo) Build()
    {
        var repo = new NoWriteMessageRepo();
        // These tests only exercise SendAsync's early validation, which throws before
        // touching any of the template-send dependencies below — never constructed here.
        var service = new ManualSendService(
            repo,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            null!, null!, null!, null!, null!,
            NullLogger<ManualSendService>.Instance);
        return (service, repo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(",")]
    [InlineData(" , , ")]
    public async Task A_send_with_no_usable_recipient_is_refused_before_anything_is_queued(
        string toNumbers)
    {
        var (service, repo) = Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendAsync(new ManualSendRequest
            {
                ChannelType = ChannelType.SMS,
                ToNumbers = toNumbers,
                Message = "hello"
            }));

        Assert.Contains("at least one recipient", ex.Message);
        // Nothing was written, so no request sits in the log waiting to fail.
        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task A_send_with_no_message_is_refused()
    {
        var (service, repo) = Build();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendAsync(new ManualSendRequest
            {
                ChannelType = ChannelType.SMS,
                ToNumbers = "8801748848039",
                Message = "   "
            }));

        Assert.Contains("message to send", ex.Message);
        Assert.Empty(repo.Added);
    }

    [Fact]
    public async Task A_voice_send_needs_a_campaign_rather_than_message_text()
    {
        // Voice dials a campaign; the body is not what the provider reads, so an empty
        // message must not block a valid voice send.
        var (service, repo) = Build();

        await service.SendAsync(new ManualSendRequest
        {
            ChannelType = ChannelType.Voice,
            ToNumbers = "8801748848039",
            Message = "",
            CampaignId = "due-bill-api"
        });

        Assert.Single(repo.Added);
    }

    [Fact]
    public async Task A_voice_send_with_no_campaign_uses_the_channels_own_and_is_accepted()
    {
        // The Voice tab says "leave empty to use the channel's campaign"; the API used to
        // answer that with "Enter a message to send."
        var (service, repo) = Build();

        await service.SendAsync(new ManualSendRequest
        {
            ChannelType = ChannelType.Voice,
            ToNumbers = "8801748848039"
        });

        Assert.Single(repo.Added);
    }

    [Fact]
    public async Task A_campaign_id_does_not_stand_in_for_a_message_on_sms()
    {
        var (service, repo) = Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SendAsync(new ManualSendRequest
            {
                ChannelType = ChannelType.SMS,
                ToNumbers = "8801748848039",
                CampaignId = "due-bill-api"
            }));

        Assert.Empty(repo.Added);
    }
}
