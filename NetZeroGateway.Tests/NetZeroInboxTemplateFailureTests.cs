using System.Text.Json;
using NetZeroGateway.Infrastructure.HttpClients.Models;

namespace NetZeroGateway.Tests;

/// <summary>
/// A real refusal captured from the vendor on 2026-09-25 (recipient not on the WhatsApp Business
/// test list). It arrives as success:false with the reason buried in metaResponse.error, and used
/// to be stored as a bare "unknown_error".
/// </summary>
public class NetZeroInboxTemplateFailureTests
{
    private const string RealRefusal =
        """{"success":false,"metaResponse":{"error":{"message":"(#131030) Recipient phone number not in allowed list","code":131030,"type":"OAuthException","error_data":{"messaging_product":"whatsapp","details":"Recipient phone number not in allowed list: Add recipient phone number to recipient list and try again."},"fbtrace_id":"AhrIZeFqvkaqb6ybJmZkawF"}}}""";

    [Fact]
    public void A_meta_refusal_is_a_failure_with_the_real_reason()
    {
        var r = JsonSerializer.Deserialize<NetZeroInboxTemplateResponse>(RealRefusal)!;

        Assert.False(r.IsSuccess);
        Assert.Contains("not in allowed list", r.FailureReason);
        Assert.Contains("131030", r.FailureReason);
    }

    [Fact]
    public void The_detail_sentence_is_preferred_because_it_says_what_to_do()
    {
        var r = JsonSerializer.Deserialize<NetZeroInboxTemplateResponse>(RealRefusal)!;

        Assert.Contains("Add recipient phone number", r.FailureReason);
    }

    [Fact]
    public void An_invalid_key_reply_still_reads_as_its_message()
    {
        var r = JsonSerializer.Deserialize<NetZeroInboxTemplateResponse>(
            """{"success":false,"message":"Invalid API keys found"}""")!;

        Assert.Equal("Invalid API keys found", r.FailureReason);
    }

    [Fact]
    public void A_real_success_is_unchanged()
    {
        var r = JsonSerializer.Deserialize<NetZeroInboxTemplateResponse>(
            """{"success":true,"metaResponse":{"messaging_product":"whatsapp","messages":[{"id":"wamid.X","message_status":"accepted"}]}}""")!;

        Assert.True(r.IsSuccess);
        Assert.Equal("wamid.X", r.MessageId);
    }
}
