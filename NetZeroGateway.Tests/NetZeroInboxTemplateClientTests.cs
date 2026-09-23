using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Infrastructure.HttpClients;
using System.Net;
using System.Text.Json;

namespace NetZeroGateway.Tests;

/// <summary>
/// Hotfix batch 3, task 7 first shipped this without the query-string duplicates seen in
/// the vendor's example curl, guessing they were leftover Postman export noise, and with a
/// response shape guessed from the vendor's documentation rather than a real call. Hotfix
/// batch 4's real test against the vendor came back with every response field null on the
/// first attempt (fixed by adding the query-string duplicates and enableLog), and even
/// after that fix a genuinely successful, delivered send still read as "failed" — the real
/// response shape is <c>{"success":true,"metaResponse":{"messages":[{"id":"wamid...",
/// "message_status":"accepted"}]}}</c>, not the flat "status"/"message_id" shape assumed
/// before. These pin the corrected request shape and the real response shape, the second
/// one using the exact JSON captured from the vendor in that session.
/// </summary>
public class NetZeroInboxTemplateClientTests
{
    /// <summary>Captured verbatim from Seq during the hotfix batch 4 live test.</summary>
    private const string RealSuccessResponse =
        """{"success":true,"metaResponse":{"messaging_product":"whatsapp","contacts":[{"input":"8801749090963","wa_id":"8801749090963"}],"messages":[{"id":"wamid.HBgNODgwMTc0OTA5MDk2MxUCABEYEjA0QjNFNUJGNkNCRDQ0MUY0QwA=","message_status":"accepted"}]}}""";

    private sealed class CapturingHandler(string responseBody = RealSuccessResponse) : HttpMessageHandler
    {
        public string? CapturedBody { get; private set; }
        public Uri? CapturedUri { get; private set; }
        public string? CapturedAuthHeader { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            CapturedUri = request.RequestUri;
            CapturedAuthHeader = request.Headers.Authorization?.ToString();
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(responseBody) };
        }
    }

    [Fact]
    public async Task Sends_the_vendor_spelling_bearer_header_query_duplicates_and_enableLog()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://inbox.netzero.com.bd") };
        var client = new NetZeroInboxClient(
            http, new ConfigurationBuilder().Build(), NullLogger<NetZeroInboxClient>.Instance);

        var result = await client.SendTemplateAsync(
            "tmpl-token-1", "+8801749090963", "due_bill_reminder",
            ["John Doe", "1500"], "https://example.com/invoice.pdf");

        Assert.Equal("Bearer tmpl-token-1", handler.CapturedAuthHeader);
        Assert.Contains("Content-Type=application%2Fjson", handler.CapturedUri!.Query);
        Assert.Contains("Authorization=Bearer", handler.CapturedUri!.Query);
        Assert.True(result.IsSuccess);
        Assert.StartsWith("wamid.", result.MessageId);

        using var doc = JsonDocument.Parse(handler.CapturedBody!);
        var root = doc.RootElement;
        Assert.Equal("+8801749090963", root.GetProperty("sendTo").GetString());
        // The vendor's own field name, kept as-is rather than "corrected" to "templateName".
        Assert.Equal("due_bill_reminder", root.GetProperty("templetName").GetString());
        Assert.Equal("tmpl-token-1", root.GetProperty("token").GetString());
        Assert.True(root.GetProperty("enableLog").GetBoolean());
        Assert.Equal(
            ["John Doe", "1500"],
            root.GetProperty("exampleArr").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task A_real_successful_response_is_read_as_success_with_the_real_message_id()
    {
        // The exact bug found live: success:true plus a nested messages array, not the
        // flat status/message_id shape assumed before a real call was ever made.
        var handler = new CapturingHandler(RealSuccessResponse);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://inbox.netzero.com.bd") };
        var client = new NetZeroInboxClient(
            http, new ConfigurationBuilder().Build(), NullLogger<NetZeroInboxClient>.Instance);

        var result = await client.SendTemplateAsync(
            "tmpl-token-1", "+8801749090963", "hello_world_api_test", ["a"], null);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            "wamid.HBgNODgwMTc0OTA5MDk2MxUCABEYEjA0QjNFNUJGNkNCRDQ0MUY0QwA=",
            result.MessageId);
    }

    [Fact]
    public async Task Omits_mediaUri_entirely_when_not_set_rather_than_sending_json_null()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://inbox.netzero.com.bd") };
        var client = new NetZeroInboxClient(
            http, new ConfigurationBuilder().Build(), NullLogger<NetZeroInboxClient>.Instance);

        await client.SendTemplateAsync("tmpl-token-1", "+8801749090963", "due_bill_reminder", ["a"], null);

        using var doc = JsonDocument.Parse(handler.CapturedBody!);
        Assert.False(doc.RootElement.TryGetProperty("mediaUri", out _));
    }

    [Fact]
    public async Task A_non_success_status_is_reported_as_a_failure_not_silently_parsed()
    {
        var http = new HttpClient(new ErrorHandler()) { BaseAddress = new Uri("https://inbox.netzero.com.bd") };
        var client = new NetZeroInboxClient(
            http, new ConfigurationBuilder().Build(), NullLogger<NetZeroInboxClient>.Instance);

        var result = await client.SendTemplateAsync("tmpl-token-1", "+8801749090963", "due_bill_reminder", ["a"], null);

        Assert.False(result.IsSuccess);
        Assert.Contains("500", result.Error);
    }

    private sealed class ErrorHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("Internal Server Error")
            });
    }
}
