using System.Text.Json;
using NetZeroGateway.Application.Services;

namespace NetZeroGateway.Tests;

/// <summary>
/// The seeder used to replace an existing channel's whole config on every boot, which
/// reverted admin edits and dropped fields the seed never carried. Only credentials
/// refresh from the seed now.
/// </summary>
public class ChannelConfigSeedMergeTests
{
    private static Dictionary<string, string> Merge(string? existing, Dictionary<string, string> seed)
        => JsonSerializer.Deserialize<Dictionary<string, string>>(
            ChannelConfigService.MergeSeededConfig(existing, seed))!;

    [Fact]
    public void Credentials_refresh_from_the_seed()
    {
        var result = Merge("""{"apikey":"old","callerID":"x"}""",
            new() { ["apikey"] = "rotated", ["callerID"] = "x" });

        Assert.Equal("rotated", result["apikey"]);
    }

    [Fact]
    public void An_admin_edit_to_a_non_credential_field_survives()
    {
        var result = Merge("""{"apikey":"k","callerID":"edited"}""",
            new() { ["apikey"] = "k", ["callerID"] = "seeded" });

        Assert.Equal("edited", result["callerID"]);
    }

    [Fact]
    public void A_field_the_seed_never_carried_survives()
    {
        var result = Merge("""{"token":"t","templateToken":"added-by-admin","balanceClientId":"c"}""",
            new() { ["token"] = "t" });

        Assert.Equal("added-by-admin", result["templateToken"]);
        Assert.Equal("c", result["balanceClientId"]);
    }

    [Fact]
    public void A_field_missing_from_the_stored_config_is_filled_from_the_seed()
    {
        var result = Merge("""{"token":"t"}""",
            new() { ["token"] = "t", ["baseUrl"] = "https://provider.example" });

        Assert.Equal("https://provider.example", result["baseUrl"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not json")]
    public void An_unreadable_stored_config_is_replaced_by_the_seed(string? existing)
    {
        var result = Merge(existing, new() { ["token"] = "t", ["baseUrl"] = "b" });

        Assert.Equal("t", result["token"]);
        Assert.Equal("b", result["baseUrl"]);
    }

    [Fact]
    public void An_unchanged_config_serialises_identically_so_the_seeder_sees_no_change()
    {
        const string stored = """{"token":"t","baseUrl":"b","extra":"e"}""";

        Assert.Equal(stored,
            ChannelConfigService.MergeSeededConfig(stored, new Dictionary<string, string> { ["token"] = "t", ["baseUrl"] = "b" }));
    }
}
