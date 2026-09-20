using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Infrastructure.HttpClients;
using System.Net;
using System.Text.Json;

namespace NetZeroGateway.Tests;

/// <summary>
/// Hotfix batch 3, task 2 asked for destinationNumbers to be sent as a real JSON array
/// rather than a comma-joined string. Investigating found the wire format was already a
/// JSON array (System.Text.Json over a List&lt;string&gt;) — no failure reproduces in code
/// or in Seq's logged history. This pins the correct shape so a future regression here
/// would fail a test instead of only showing up in production.
/// </summary>
public class NetZeroVoiceClientTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? CapturedBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            CapturedBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"status":"success","message":"queued","result":{"taskId":"t-1"}}""")
            };
        }
    }

    [Fact]
    public async Task Multi_number_campaigns_serialize_destinationNumbers_as_a_json_array()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://voice.example.test") };
        var client = new NetZeroVoiceClient(
            http, new ConfigurationBuilder().Build(), NullLogger<NetZeroVoiceClient>.Instance);

        await client.RunCampaignAsync(
            "owner", "password", "auth-user", "due-bill-api", "1",
            ["8801302020603", "8801302798305", "8801304098718"]);

        Assert.NotNull(handler.CapturedBody);
        using var doc = JsonDocument.Parse(handler.CapturedBody!);
        var numbers = doc.RootElement.GetProperty("destinationNumbers");
        Assert.Equal(JsonValueKind.Array, numbers.ValueKind);
        Assert.Equal(
            ["8801302020603", "8801302798305", "8801304098718"],
            numbers.EnumerateArray().Select(n => n.GetString()));
    }
}
