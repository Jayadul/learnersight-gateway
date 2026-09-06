using NetZeroGateway.Application.Utilities;

namespace NetZeroGateway.Tests;

/// <summary>
/// GenNet rejects a CSMS ID that is not exactly 20 characters with status_code 4020
/// over HTTP 200 — so it surfaces as a delivery failure, not a malformed request. The
/// obvious choice, a 32-character GUID, fails every single send.
/// </summary>
public class CsmsIdTests
{
    [Fact]
    public void An_id_is_exactly_twenty_characters()
        => Assert.Equal(20, CsmsId.New().Length);

    [Fact]
    public void An_id_is_uppercase_letters_and_digits_only()
    {
        // Lowercase and punctuation are untested against the vendors' validator.
        var id = CsmsId.New();
        Assert.All(id, c => Assert.True(
            (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'),
            $"'{c}' is not an uppercase letter or digit"));
    }

    [Fact]
    public void Ids_do_not_repeat()
    {
        // Each send needs its own; a repeat would be rejected as a duplicate batch.
        var ids = Enumerable.Range(0, 500).Select(_ => CsmsId.New()).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
