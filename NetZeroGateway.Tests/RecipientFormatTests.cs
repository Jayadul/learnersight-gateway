using NetZeroGateway.Application.Utilities;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// Recipient numbers are stored canonically (digits, country code, no plus). Two of the
/// Release 3 providers disagree with that in opposite directions, and one that shares a
/// vendor platform with another does not — so the format is a per-provider fact rather
/// than something to infer. Getting it wrong sends to the wrong number or is rejected.
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
    public void GenNet_takes_the_local_form()
        => Assert.Equal("01748848039", RecipientFormat.For(Provider.GenNet, Canonical));

    [Fact]
    public void NetZero_Inbox_takes_a_leading_plus()
        => Assert.Equal("+8801748848039", RecipientFormat.For(Provider.NetZeroInbox, Canonical));

    [Fact]
    public void Chithi_takes_the_stored_form_even_though_it_shares_GenNets_credentials()
    {
        // The two vendors issue one api_token and one sid between them and their APIs are
        // the same family, which makes it tempting to give them one implementation. Their
        // documented request bodies disagree on this, so they do not share one.
        Assert.Equal(Canonical, RecipientFormat.For(Provider.Chithi, Canonical));
        Assert.NotEqual(
            RecipientFormat.For(Provider.GenNet, Canonical),
            RecipientFormat.For(Provider.Chithi, Canonical));
    }

    [Theory]
    [InlineData("8801748848039", "01748848039")]
    [InlineData("01748848039", "01748848039")]   // already local, left alone
    public void ToLocal_drops_the_country_code_and_is_idempotent(string input, string expected)
        => Assert.Equal(expected, RecipientFormat.ToLocal(input));

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
