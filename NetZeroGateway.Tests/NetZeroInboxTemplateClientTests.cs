using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Infrastructure.HttpClients;
using System.Net;
using System.Text.Json;

namespace NetZeroGateway.Tests;

/// <summary>
/// Hotfix batch 3, task 7: pins the WhatsApp Template request shape — the vendor's own
/// "templet" spelling kept verbatim in the wire payload, the Bearer header, and that the
/// query-string duplicates shown in the vendor's example curl are NOT sent (they read as
/// leftover Postman export noise, not a real requirement).
/// </summary>
public class NetZeroInboxTemplateClientTests
{
    private sealed class CapturingHandler : HttpMessageHandler
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
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":"success","metaResponse":{"message_id":"wamid.TEMPLATE1"}}""")
            };
        }
    }

    [Fact]
    public async Task Sends_the_vendor_spelling_a_bearer_header_and_no_query_string_duplicates()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://inbox.netzero.com.bd") };
        var client = new NetZeroInboxClient(
            http, new ConfigurationBuilder().Build(), NullLogger<NetZeroInboxClient>.Instance);

        var result = await client.SendTemplateAsync(
            "tmpl-token-1", "+8801749090963", "due_bill_reminder",
            ["John Doe", "1500"], "https://example.com/invoice.pdf");

        Assert.Equal("Bearer tmpl-token-1", handler.CapturedAuthHeader);
        Assert.Equal("", handler.CapturedUri!.Query); // no query-string duplicates of Content-Type/Authorization/token
        Assert.Equal("wamid.TEMPLATE1", result.MessageId);
        Assert.True(result.IsSuccess);

        using var doc = JsonDocument.Parse(handler.CapturedBody!);
        var root = doc.RootElement;
        Assert.Equal("+8801749090963", root.GetProperty("sendTo").GetString());
        // The vendor's own field name, kept as-is rather than "corrected" to "templateName".
        Assert.Equal("due_bill_reminder", root.GetProperty("templetName").GetString());
        Assert.Equal("tmpl-token-1", root.GetProperty("token").GetString());
        Assert.Equal(
            ["John Doe", "1500"],
            root.GetProperty("exampleArr").EnumerateArray().Select(e => e.GetString()));
    }
}
