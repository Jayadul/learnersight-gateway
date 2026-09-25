using NetZeroGateway.Application.Utilities;

namespace NetZeroGateway.Tests;

/// <summary>
/// The channel's own base URL decides where a send goes. Until Release 3 the base URL was
/// bound once per channel type at DI registration, which made two vendors on one type
/// impossible — and left the admin UI's baseUrl field doing nothing. See ADR 0008.
/// </summary>
public class ProviderEndpointTests
{
    [Fact]
    public void A_channel_without_a_base_url_falls_back_to_the_registered_default()
    {
        // Returning the bare path lets HttpClient.BaseAddress apply, which is what every
        // channel created before R3 relies on.
        Assert.Equal("/sendtext", ProviderEndpoint.Resolve(null, "/sendtext"));
        Assert.Equal("/sendtext", ProviderEndpoint.Resolve("", "/sendtext"));
        Assert.Equal("/sendtext", ProviderEndpoint.Resolve("   ", "/sendtext"));
    }

    [Fact]
    public void A_channel_with_a_base_url_gets_an_absolute_endpoint()
    {
        Assert.Equal(
            "https://isms.gennet.com.bd/api/v3/send-sms/bulk",
            ProviderEndpoint.Resolve("https://isms.gennet.com.bd", "/api/v3/send-sms/bulk"));
    }

    [Fact]
    public void A_trailing_slash_on_the_base_url_does_not_double_up()
    {
        Assert.Equal(
            "http://smpp.revesms.com:7788/sendtext",
            ProviderEndpoint.Resolve("http://smpp.revesms.com:7788/", "/sendtext"));
    }

    [Fact]
    public void A_malformed_base_url_falls_back_instead_of_throwing()
    {
        // One channel's typo must fail that channel's send with a provider error the
        // operator can read — not take down every channel sharing the HTTP client.
        Assert.Equal("/sendtext", ProviderEndpoint.Resolve("not a url", "/sendtext"));
    }

    // The Voice host is written as a bare host, obd.netzero.com.bd, with no scheme or port (hotfix
    // batch 7). Not an absolute URL, so a channel used to ignore it and, as the registered default,
    // it would have stopped the API starting.

    [Fact]
    public void A_bare_host_is_read_as_http()
        => Assert.Equal("http://obd.netzero.com.bd", ProviderEndpoint.Normalize("obd.netzero.com.bd"));

    [Fact]
    public void A_full_url_is_left_alone_by_normalize()
    {
        Assert.Equal("http://obd.netzero.com.bd:8000", ProviderEndpoint.Normalize("http://obd.netzero.com.bd:8000"));
        Assert.Equal("https://x.example.com", ProviderEndpoint.Normalize(" https://x.example.com "));
    }

    [Fact]
    public void A_channel_with_a_bare_host_sends_to_that_host_not_the_default()
        => Assert.Equal(
            "http://obd.netzero.com.bd/api/v1/run-campaign",
            ProviderEndpoint.Resolve("obd.netzero.com.bd", "/api/v1/run-campaign"));

    [Fact]
    public void A_port_written_after_a_bare_host_is_kept()
        => Assert.Equal(
            "http://obd.netzero.com.bd:8000/api/v1/combined-report",
            ProviderEndpoint.Resolve("obd.netzero.com.bd:8000", "/api/v1/combined-report"));

    [Fact]
    public void BaseUrlOf_reads_the_channel_config_and_treats_blank_as_unset()
    {
        Assert.Equal(
            "https://example.test",
            ProviderEndpoint.BaseUrlOf(new Dictionary<string, string>
            {
                ["baseUrl"] = "https://example.test"
            }));

        Assert.Null(ProviderEndpoint.BaseUrlOf(new Dictionary<string, string>
        {
            ["baseUrl"] = "  "
        }));

        Assert.Null(ProviderEndpoint.BaseUrlOf(new Dictionary<string, string>
        {
            ["apikey"] = "k"
        }));
    }
}
