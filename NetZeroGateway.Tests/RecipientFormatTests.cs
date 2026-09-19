using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Recipient numbers are stored canonically (digits, country code, no plus). NetZero
/// Inbox's v1 API disagrees with that, wanting a leading plus, while its own QR API on
/// the same host does not — so the format is a per-provider fact rather than something
/// to infer. Getting it wrong sends to the wrong number or is rejected.
/// </summary>
public class RecipientFormatTests
{
    private const string Canonical = "8801748848039";

    [Fact]
    public void Providers_that_take_the_stored_form_get_it_unchanged()
    {
        Assert.Equal(Canonical, RecipientFormat.For(Provider.Reve, Canonical));
        Assert.Equal(Canonical, RecipientFormat.For(Provider.WaTrend, Canonical));
        Assert.Equal(Canonical, RecipientFormat.For(Provider.NetZeroVoice, Canonical));
    }

    [Fact]
    public void NetZero_Inbox_takes_a_leading_plus()
        => Assert.Equal("+8801748848039", RecipientFormat.For(Provider.NetZeroInbox, Canonical));

    [Fact]
    public void Chithi_takes_the_stored_form()
        => Assert.Equal(Canonical, RecipientFormat.For(Provider.Chithi, Canonical));

    [Fact]
    public void The_two_NetZero_Inbox_APIs_disagree_about_the_plus()
    {
        // Same vendor, same host, same token — but v1 wants a leading plus and the QR
        // device API does not. Sending either form to the other API is a wrong number.
        Assert.Equal("+8801748848039", RecipientFormat.For(Provider.NetZeroInbox, Canonical));
        Assert.Equal("8801748848039", RecipientFormat.For(Provider.NetZeroInboxQr, Canonical));
        Assert.NotEqual(
            RecipientFormat.For(Provider.NetZeroInbox, Canonical),
            RecipientFormat.For(Provider.NetZeroInboxQr, Canonical));
    }

    [Theory]
    [InlineData("8801748848039", "8801748848039")]
    [InlineData("+8801748848039", "8801748848039")]
    [InlineData("01748848039", "8801748848039")]
    [InlineData("+880 1748-848039", "8801748848039")]
    public void ToCanonical_maps_every_rendering_back_to_the_stored_form(
        string input, string expected)
        => Assert.Equal(expected, RecipientFormat.ToCanonical(input));

    [Fact]
    public void The_local_trunk_zero_is_replaced_by_the_country_code_not_kept()
    {
        // 01748848039 must become 8801748848039, never 88001748848039 — an extra zero
        // is a different number, and the provider would either reject it or deliver
        // somewhere unintended.
        Assert.Equal("8801748848039", RecipientFormat.ToCanonical("01748848039"));
        Assert.DoesNotContain("8800", RecipientFormat.ToCanonical("01748848039"));
    }

    [Fact]
    public void Round_tripping_through_a_providers_form_returns_the_stored_number()
    {
        foreach (var provider in Enum.GetValues<Provider>())
        {
            var onTheWire = RecipientFormat.For(provider, Canonical);
            Assert.Equal(Canonical, RecipientFormat.ToCanonical(onTheWire));
        }
    }
}
