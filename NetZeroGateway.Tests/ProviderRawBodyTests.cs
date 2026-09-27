using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NetZeroGateway.Infrastructure.HttpClients;
using System.Net;

namespace NetZeroGateway.Tests;

/// <summary>
/// Hotfix batch 8: the WhatsApp QR and Template channels showed a Raw Provider Response that was
/// the gateway's own model re-serialized (every field, most of them null, nested data dropped),
/// not what the provider sent. The clients now keep the body they received, verbatim, for the
/// senders to store. The bodies below are the real ones captured in the batch 8 live sends.
/// </summary>
public class ProviderRawBodyTests
{
    private const string QrSuccess =
        """{"success":true,"message":"Message sent successfully","data":{"messageId":"3EB02553CF2BEFEECDEE47","recipient":"8801748848039"},"other":{"key":{"fromMe":true},"status":"PENDING"}}""";
    private const string QrNotOnWhatsApp =
        """{"success":false,"message":"This number is not found on WhatsApp","solution":"Please give a number which is available on WhatsApp."}""";
    private const string TemplateMetaError =
        """{"success":false,"metaResponse":{"error":{"message":"(#131030) Recipient phone number not in allowed list","code":131030,"type":"OAuthException"}}}""";

    private sealed class Reply(HttpStatusCode code, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body) });
    }

    private static HttpClient Http(HttpStatusCode code, string body)
        => new(new Reply(code, body)) { BaseAddress = new Uri("https://provider.example") };

    private static readonly IConfiguration Config = new ConfigurationBuilder().Build();

    [Theory]
    [InlineData(HttpStatusCode.OK, QrSuccess)]
    [InlineData(HttpStatusCode.OK, QrNotOnWhatsApp)]
    [InlineData(HttpStatusCode.BadRequest, QrNotOnWhatsApp)]
    public async Task Qr_keeps_the_provider_body_verbatim(HttpStatusCode code, string body)
    {
        var client = new NetZeroInboxQrClient(Http(code, body), Config, NullLogger<NetZeroInboxQrClient>.Instance);

        var result = await client.SendTextAsync("tok", "8801000000000", "8801748848039", "hi");

        Assert.Equal(body, result.RawBody);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK, TemplateMetaError)]
    [InlineData(HttpStatusCode.BadRequest, TemplateMetaError)]
    public async Task Template_keeps_the_nested_meta_response_verbatim(HttpStatusCode code, string body)
    {
        var client = new NetZeroInboxClient(Http(code, body), Config, NullLogger<NetZeroInboxClient>.Instance);

        var result = await client.SendTemplateAsync("tok", "+8801748848039", "hello_world_api_test", ["x"], null);

        Assert.Equal(body, result.RawBody);
        Assert.Contains("\"code\":131030", result.RawBody);
    }

    [Fact]
    public async Task Inbox_text_keeps_the_provider_body_verbatim()
    {
        const string body = """{"success":false,"error":null,"message":"Invalid API keys found","metaResponse":null}""";
        var client = new NetZeroInboxClient(Http(HttpStatusCode.OK, body), Config, NullLogger<NetZeroInboxClient>.Instance);

        var result = await client.SendTextAsync("tok", "8801748848039", "hi");

        Assert.Equal(body, result.RawBody);
    }
}
