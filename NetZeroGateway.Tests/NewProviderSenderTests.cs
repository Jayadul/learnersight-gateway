using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Domain.Entities;
using NetZeroGateway.Domain.Enums;
using NetZeroGateway.Infrastructure.HttpClients;
using NetZeroGateway.Infrastructure.HttpClients.Models;
using NetZeroGateway.Infrastructure.Strategies;

namespace NetZeroGateway.Tests;

/// <summary>
/// The providers added in Release 3. What each sends on the wire is the part worth
/// pinning: several disagree with our stored number format in different directions, one
/// batches where another does not, and two share a vendor and a token while agreeing on
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

    // ── GenNet ───────────────────────────────────────────────────────────────

    private sealed class FakeGenNetClient(bool success = true) : IGenNetClient
    {
        public List<string>? SentTo { get; private set; }
        public string? BatchId { get; private set; }

        public Task<GenNetSendResponse> SendBulkAsync(
            string apiToken, string sid, List<string> msisdns, string message,
            string batchCsmsId, string? baseUrl = null, CancellationToken ct = default)
        {
            SentTo = msisdns;
            BatchId = batchCsmsId;
            return Task.FromResult(new GenNetSendResponse
            {
                Status = success ? "SUCCESS" : "FAILED",
                ErrorMessage = success ? null : "Insufficient balance",
                // The vendor echoes numbers back in country-code form and gives each
                // its own reference id.
                SmsInfo = success
                    ? [.. msisdns.Select((m, i) => new GenNetSmsInfo
                    {
                        Msisdn = "880" + m.TrimStart('0'),
                        ReferenceId = $"REF{i + 1}"
                    })]
                    : null
            });
        }

        public Task<GenNetBalanceResponse> GetBalanceAsync(
            string apiToken, string? baseUrl = null, CancellationToken ct = default)
            => Task.FromResult(new GenNetBalanceResponse { Status = "SUCCESS" });
    }

    [Fact]
    public async Task GenNet_sends_every_recipient_in_one_call_in_local_format()
    {
        var client = new FakeGenNetClient();
        var sender = new GenNetSmsSender(client, NullLogger<GenNetSmsSender>.Instance);
        var job = SmsJob("8801748848039,8801969155575");

        await sender.SendAsync(job, Cfg());

        // One bulk call, and the country code is stripped — GenNet's documented form.
        Assert.Equal(["01748848039", "01969155575"], client.SentTo);
        Assert.Equal(JobStatus.Sent, job.Status);
    }

    [Fact]
    public async Task GenNet_records_the_batch_id_it_generated()
    {
        var client = new FakeGenNetClient();
        var sender = new GenNetSmsSender(client, NullLogger<GenNetSmsSender>.Instance);
        var job = SmsJob("8801748848039");

        await sender.SendAsync(job, Cfg());

        // The batch id is the only handle identifying this job in GenNet's system.
        Assert.Equal(client.BatchId, job.ProviderMessageId);
        Assert.False(string.IsNullOrWhiteSpace(job.ProviderMessageId));
    }

    [Fact]
    public async Task GenNet_surfaces_the_providers_own_reason_on_failure()
    {
        var sender = new GenNetSmsSender(
            new FakeGenNetClient(success: false), NullLogger<GenNetSmsSender>.Instance);
        var job = SmsJob("8801748848039");

        await sender.SendAsync(job, Cfg());

        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Equal("Insufficient balance", job.FailureReason);
    }

    [Fact]
    public async Task GenNet_maps_each_recipients_own_reference_id_onto_its_row()
    {
        // The batch id identifies the job, not any one message. Keeping only that would
        // discard exactly the detail a bulk response exists to give.
        var client = new FakeGenNetClient();
        var sender = new GenNetSmsSender(client, NullLogger<GenNetSmsSender>.Instance);
        var job = SmsJob("8801748848039,8801969155575");

        await sender.SendAsync(job, Cfg());

        Assert.NotNull(job.RecipientReferences);
        // Keyed by the stored form, though the provider echoed its own.
        Assert.Equal("REF1", job.RecipientReferences!["8801748848039"]);
        Assert.Equal("REF2", job.RecipientReferences!["8801969155575"]);
    }

    [Fact]
    public async Task GenNet_reports_no_per_recipient_references_when_the_send_failed()
    {
        var sender = new GenNetSmsSender(
            new FakeGenNetClient(success: false), NullLogger<GenNetSmsSender>.Instance);
        var job = SmsJob("8801748848039");

        await sender.SendAsync(job, Cfg());

        Assert.Null(job.RecipientReferences);
    }

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

        // Two calls, not one, and the stored form is kept — unlike GenNet, despite the
        // two vendors sharing an API family and credentials.
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

        public Task<NetZeroInboxQrResponse> SendTextAsync(
            string token, string from, string to, string message,
            string? baseUrl = null, CancellationToken ct = default)
        {
            WasCalled = true;
            SentTo = to;
            SentFrom = from;
            return Task.FromResult(new NetZeroInboxQrResponse { Id = "QR-1" });
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
    public async Task The_QR_API_also_refuses_media()
    {
        var client = new FakeInboxQrClient();
        var sender = new NetZeroInboxQrWhatsAppSender(
            client, NullLogger<NetZeroInboxQrWhatsAppSender>.Instance);
        var job = WhatsAppJob("8801748848039", mediaUrl: "https://example.com/f.pdf");

        await sender.SendAsync(job, QrCfg());

        Assert.False(client.WasCalled);
        Assert.Equal(JobStatus.Failed, job.Status);
        Assert.Contains("does not support media", job.FailureReason);
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
