using System.Reflection;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.Tests;

/// <summary>
/// Which channel config fields are masked on read. This was an exact list of the five
/// field names the first three providers used, so every provider added afterwards served
/// its credential in the clear through the admin API until someone extended the list —
/// which is exactly what happened when GenNet, Chithi and NetZero Inbox were seeded.
/// These tests pin the shape-based rule so the safe behaviour stays the default.
/// </summary>
public class ChannelConfigSecretMaskingTests
{
    // IsSecret is private: it is an internal detail of ToDto, but the consequence of
    // getting it wrong is a leaked credential, so it is worth testing directly rather
    // than through a service that needs a repository and an encryptor.
    private static bool IsSecret(string key) =>
        (bool)typeof(ChannelConfigService)
            .GetMethod("IsSecret", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [key])!;

    [Theory]
    [InlineData("apikey")]
    [InlineData("secretkey")]
    [InlineData("access_token")]
    [InlineData("password")]
    [InlineData("authUserId")]
    public void The_original_provider_credentials_are_still_masked(string key)
        => Assert.True(IsSecret(key));

    [Theory]
    [InlineData("api_token")]            // GenNet, Chithi
    [InlineData("token")]                // NetZero Inbox
    [InlineData("balanceSessionCookie")] // REVE balance
    public void Credentials_added_after_the_original_three_providers_are_masked(string key)
        => Assert.True(IsSecret(key));

    [Theory]
    [InlineData("baseUrl")]
    [InlineData("callerID")]
    [InlineData("instance_id")]
    [InlineData("sid")]
    [InlineData("balanceClientId")]
    [InlineData("campaignId")]
    [InlineData("lotNumber")]
    [InlineData("owner")]
    public void Identifiers_an_operator_needs_to_read_stay_visible(string key)
        => Assert.False(IsSecret(key));

    [Fact]
    public void Matching_is_case_insensitive()
    {
        // Provider field names are whatever the vendor's API calls them, so casing is
        // not ours to rely on.
        Assert.True(IsSecret("API_TOKEN"));
        Assert.True(IsSecret("SecretKey"));
        Assert.True(IsSecret("Cookie"));
    }
}
