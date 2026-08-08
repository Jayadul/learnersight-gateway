using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// The per-message lot number is deliberately not persisted on DispatchJob — it is
/// overlaid onto the channel config handed to the sender. That overlay is the only
/// thing carrying <c>[call::campaignId||lotNumber]</c> through to the provider, so it
/// is covered here rather than by an integration test that would call a live provider.
/// </summary>
public class ChannelDispatchServiceLotNumberTests
{
    private const string ConfiguredLot = "CFG_DEFAULT_LOT";

    private static (ChannelDispatchService Service, CapturingSender Sender) Build()
    {
        var voiceConfig = new ChannelConfig
        {
            ChannelType = ChannelType.Voice,
            ProviderName = "test",
            Label = "Voice Test",
            IsActive = true,
            // PlainTextEncryption is identity, so this is stored readable on purpose.
            ConfigJson =
                $$"""
                  {"owner":"o","password":"p","authUserId":"a","campaignId":"CFG_CAMPAIGN","lotNumber":"{{ConfiguredLot}}"}
                  """
        };

        var sender = new CapturingSender(ChannelType.Voice);

        var service = new ChannelDispatchService(
            new FakeMessageRequestRepository(),
            new FakeDispatchJobRepository(),
            new FakeChannelConfigRepository(voiceConfig),
            new PlainTextEncryption(),
            new FakeSenderFactory(sender),
            new NoOpFallbackHandler(),
            NullLogger<ChannelDispatchService>.Instance);

        return (service, sender);
    }

    private static ParsedChannelJob VoiceJob(string? lotNumber) => new()
    {
        ChannelType = ChannelType.Voice,
        Message = "CAMP1",
        CampaignId = "CAMP1",
        LotNumber = lotNumber
    };

    [Fact]
    public async Task Per_message_lot_number_overrides_the_channel_configured_lot()
    {
        var (service, sender) = Build();

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001", [VoiceJob("LOT9")]);

        Assert.NotNull(sender.CapturedConfig);
        Assert.Equal("LOT9", sender.CapturedConfig!["lotNumber"]);
    }

    [Fact]
    public async Task Omitted_lot_number_leaves_the_channel_configured_lot_intact()
    {
        var (service, sender) = Build();

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001", [VoiceJob(null)]);

        Assert.NotNull(sender.CapturedConfig);
        Assert.Equal(ConfiguredLot, sender.CapturedConfig!["lotNumber"]);
    }

    [Fact]
    public async Task Blank_lot_number_is_ignored_rather_than_blanking_the_config()
    {
        var (service, sender) = Build();

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001", [VoiceJob("   ")]);

        Assert.Equal(ConfiguredLot, sender.CapturedConfig!["lotNumber"]);
    }

    [Fact]
    public async Task Campaign_id_still_reaches_the_job_alongside_the_lot()
    {
        var (service, sender) = Build();

        await service.DispatchAsync(Guid.NewGuid(), "8801700000001", [VoiceJob("LOT9")]);

        Assert.Equal("CAMP1", sender.CapturedJob!.CampaignId);
    }
}
