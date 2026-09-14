using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using NetZeroGateway.Infrastructure.Strategies;

namespace NetZeroGateway.Tests;

/// <summary>
/// The providers added in Release 3. What each sends on the wire is the part worth
/// pinning: one disagrees with our stored number format, one batches per-call where
/// REVE batches in one call, and two share a vendor and a token while agreeing on
/// almost nothing else — none of which is visible from the job.
/// </summary>
public class NewProviderSenderTests
{
    // The channel type is carried on the job so a fixture cannot quietly describe, say,
    // a WhatsApp send as an SMS one. Senders resolve nothing from it — the factory has
    // already picked them by then — but a test that reads wrong is a test that misleads
    // the next person to change one.
    private static DispatchJob SmsJob(string toNumber) => Job(ChannelType.SMS, toNumber);

    private static DispatchJob WhatsAppJob(string toNumber, string? mediaUrl = null)
        => Job(ChannelType.WP, toNumber, mediaUrl);

    private static DispatchJob Job(
        ChannelType channelType, string toNumber, string? mediaUrl = null) => new()
    {
        MessageRequestId = Guid.NewGuid(),
        ChannelType = channelType,
        MessageContent = "hello",
        MediaUrl = mediaUrl,
        ToNumber = toNumber
    };

    // ── Chithi ───────────────────────────────────────────────────────────────

    private sealed class FakeChithiClient(params bool[] outcomes) : IChithiClient
    {
        private int _call;
        public List<string> SentTo { get; } = [];

        public Task<ChithiSendResponse> SendAsync(
            string apiToken, string sid, string msisdn, string message, string csmsId,
            string? baseUrl = null, CancellationToken ct = default)
        {
            SentTo.Add(msisdn);
            var ok = _call < outcomes.Length ? outcomes[_call] : true;
            _call++;
            return Task.FromResult(new ChithiSendResponse
            {
                Status = ok ? "SUCCESS" : "FAILED",
                ErrorMessage = ok ? null : "Invalid msisdn",
                SmsInfo = ok ? [new ChithiSmsInfo { ReferenceId = $"REF{_call}" }] : []
            });
        }

        public Task<ChithiBalanceResponse> GetBalanceAsync(
            string apiToken, string? baseUrl = null, CancellationToken ct = default)
            => Task.FromResult(new ChithiBalanceResponse { Status = "SUCCESS" });
    }

    [Fact]
    public async Task Chithi_sends_one_call_per_recipient_keeping_the_country_code()
    {
        var client = new FakeChithiClient(true, true);
        var sender = new ChithiSmsSender(client, NullLogger<ChithiSmsSender>.Instance);
        var job = SmsJob("8801748848039,8801969155575");

        await sender.SendAsync(job, Cfg());

        // Two calls, not one, and the stored form is kept.
        Assert.Equal(["8801748848039", "8801969155575"], client.SentTo);
        Assert.Equal(JobStatus.Sent, job.Status);
    }

    [Fact]
    public async Task Chithi_fails_the_job_when_any_recipient_fails_and_says_how_many()
    {
        // A job-level status cannot express "one of two arrived", so it fails and the
        // reason carries the count. The recipient rows hold the detail.
        var sender = new ChithiSmsSender(
            new FakeChithiClient(true, false), NullLogger<ChithiSmsSender>.Instance);
        var job = SmsJob("8801748848039,8801969155575");

        await sender.SendAsync(job, Cfg());

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("1 of 2", job.FailureReason);
    }

    [Fact]
    public async Task Chithi_maps_each_recipients_own_reference_id_onto_its_row()
    {
        var sender = new ChithiSmsSender(
            new FakeChithiClient(true, true), NullLogger<ChithiSmsSender>.Instance);
        var job = SmsJob("8801748848039,8801969155575");

        await sender.SendAsync(job, Cfg());

        Assert.NotNull(job.RecipientReferences);
        Assert.Equal(2, job.RecipientReferences!.Count);
        // Each came from its own call, so no two recipients share an id.
        Assert.Equal(2, job.RecipientReferences!.Values.Distinct().Count());
    }

    // ── NetZero Inbox ────────────────────────────────────────────────────────

    private sealed class FakeInboxClient : INetZeroInboxClient
    {
        public string? SentTo { get; private set; }
        public bool WasCalled { get; private set; }

        public Task<NetZeroInboxResponse> SendTextAsync(
            string token, string to, string message,
            string? baseUrl = null, CancellationToken ct = default)
        {
            WasCalled = true;
            SentTo = to;
            return Task.FromResult(new NetZeroInboxResponse
            {
                Data = new NetZeroInboxData { Id = "wamid.TEST" }
            });
        }
    }

    [Fact]
    public async Task NetZero_Inbox_sends_the_recipient_with_a_leading_plus()
    {
        var client = new FakeInboxClient();
        var sender = new NetZeroInboxWhatsAppSender(
            client, NullLogger<NetZeroInboxWhatsAppSender>.Instance);
        var job = WhatsAppJob("8801748848039");

        await sender.SendAsync(job, Cfg());

        Assert.Equal("+8801748848039", client.SentTo);
        Assert.Equal(JobStatus.Sent, job.Status);
        Assert.Equal("wamid.TEST", job.ProviderMessageId);
    }

    [Fact]
    public async Task NetZero_Inbox_refuses_a_media_send_without_calling_the_provider()
    {
        // The vendor documents no media type. Guessing one would fail inside their API
        // with an opaque error; refusing here names the channel and the reason.
        var client = new FakeInboxClient();
        var sender = new NetZeroInboxWhatsAppSender(
            client, NullLogger<NetZeroInboxWhatsAppSender>.Instance);
        var job = WhatsAppJob("8801748848039", mediaUrl: "https://example.com/f.pdf");

        await sender.SendAsync(job, Cfg());

        Assert.False(client.WasCalled);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("does not support media", job.FailureReason);
    }

    // ── NetZero Inbox, QR device API ─────────────────────────────────────────

    private sealed class FakeInboxQrClient : INetZeroInboxQrClient
    {
        public string? SentTo { get; private set; }
        public string? SentFrom { get; private set; }
        public bool WasCalled { get; private set; }
        public bool SentAsImage { get; private set; }
        public string? SentImageUrl { get; private set; }
        public string? SentCaption { get; private set; }

        public Task<NetZeroInboxQrResponse> SendTextAsync(
            string token, string from, string to, string message,
            string? baseUrl = null, CancellationToken ct = default)
        {
            WasCalled = true;
            SentTo = to;
            SentFrom = from;
            return Task.FromResult(new NetZeroInboxQrResponse { Id = "QR-1" });
        }

        public Task<NetZeroInboxQrResponse> SendImageAsync(
            string token, string from, string to, string imageUrl, string caption,
            string? baseUrl = null, CancellationToken ct = default)
        {
            WasCalled = true;
            SentAsImage = true;
            SentTo = to;
            SentFrom = from;
            SentImageUrl = imageUrl;
            SentCaption = caption;
            return Task.FromResult(new NetZeroInboxQrResponse { Id = "QR-IMG-1" });
        }
    }

    [Fact]
    public async Task The_QR_API_sends_the_recipient_without_a_plus_unlike_v1()
    {
        var client = new FakeInboxQrClient();
        var sender = new NetZeroInboxQrWhatsAppSender(
            client, NullLogger<NetZeroInboxQrWhatsAppSender>.Instance);
        var job = WhatsAppJob("8801748848039");

        await sender.SendAsync(job, QrCfg());

        Assert.Equal("8801748848039", client.SentTo);
        Assert.Equal("8801942785200", client.SentFrom);
        Assert.Equal(JobStatus.Sent, job.Status);
        Assert.Equal("QR-1", job.ProviderMessageId);
    }

    [Fact]
    public async Task The_QR_API_refuses_to_send_without_a_configured_sender_number()
    {
        // The provider cannot tell which linked WhatsApp session to use, and a blank
        // from would fail somewhere inside their API with an opaque error.
        var client = new FakeInboxQrClient();
        var sender = new NetZeroInboxQrWhatsAppSender(
            client, NullLogger<NetZeroInboxQrWhatsAppSender>.Instance);
        var job = WhatsAppJob("8801748848039");

        await sender.SendAsync(job, new Dictionary<string, string> { ["token"] = "t" });

        Assert.False(client.WasCalled);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("no sender number", job.FailureReason);
    }

    [Fact]
    public async Task The_QR_API_sends_confirmed_image_media()
    {
        var client = new FakeInboxQrClient();
        var sender = new NetZeroInboxQrWhatsAppSender(
            client, NullLogger<NetZeroInboxQrWhatsAppSender>.Instance);
        var job = WhatsAppJob("8801748848039", mediaUrl: "https://example.com/photo.jpg");

        await sender.SendAsync(job, QrCfg());

        Assert.True(client.SentAsImage);
        Assert.Equal("https://example.com/photo.jpg", client.SentImageUrl);
        Assert.Equal("hello", client.SentCaption);
        Assert.Equal(JobStatus.Sent, job.Status);
        Assert.Equal("QR-IMG-1", job.ProviderMessageId);
    }

    [Fact]
    public async Task The_QR_API_refuses_media_types_other_than_image()
    {
        // Only image is confirmed against the vendor's API. Guessing a messageType for
        // anything else risks a silent misfire rather than a clear rejection.
        var client = new FakeInboxQrClient();
        var sender = new NetZeroInboxQrWhatsAppSender(
            client, NullLogger<NetZeroInboxQrWhatsAppSender>.Instance);
        var job = WhatsAppJob("8801748848039", mediaUrl: "https://example.com/f.pdf");

        await sender.SendAsync(job, QrCfg());

        Assert.False(client.WasCalled);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("only supports image media", job.FailureReason);
    }

    private static Dictionary<string, string> QrCfg() => new()
    {
        ["token"] = "t", ["from"] = "8801942785200"
    };

    private static Dictionary<string, string> Cfg() => new()
    {
        ["api_token"] = "t", ["sid"] = "s", ["token"] = "t"
    };
}
