namespace NetZeroGateway.Application.Utilities;

/// <summary>
/// Builds the URL a provider call should go to, given the channel's own config.
/// <para>
/// Base URLs used to be bound once per channel type at DI registration, from
/// <c>Providers:*:BaseUrl</c>. That made one provider per channel type structural: two
/// SMS vendors cannot share a single <c>HttpClient.BaseAddress</c>. Since Release 3 a
/// channel may carry its own <c>baseUrl</c>, and the registered default is the fallback
/// for channels that leave it blank — which is every channel that existed before R3.
/// </para>
/// <para>
/// The admin UI has collected a <c>baseUrl</c> field on every channel since it shipped;
/// until R3 nothing read it. See ADR 0008.
/// </para>
/// </summary>
public static class ProviderEndpoint
{
    /// <summary>Config key holding a channel's own provider base URL.</summary>
    public const string BaseUrlKey = "baseUrl";

    /// <summary>
    /// Returns an absolute URL when the channel names its own base, and otherwise the
    /// bare path, which resolves against the HttpClient's registered BaseAddress.
    /// A malformed value falls back to the default rather than throwing: a typo in one
    /// channel's URL should fail that send with a provider error the operator can read,
    /// not take down every channel sharing the client.
    /// </summary>
    public static string Resolve(string? baseUrl, string path)
    {
        if (string.IsNullOrWhiteSpace(baseUrl)) return path;

        return Uri.TryCreate(Normalize(baseUrl), UriKind.Absolute, out var baseUri)
            && Uri.TryCreate(baseUri, path, out var absolute)
                ? absolute.ToString()
                : path;
    }

    /// <summary>
    /// Accepts a bare host such as <c>obd.netzero.com.bd</c> as well as a full URL, assuming
    /// <c>http://</c> when no scheme is given. Without this a host typed without a scheme was not a
    /// valid absolute URL, so a channel silently fell back to the default host, and the same value in
    /// the registered default would have stopped the API starting.
    /// </summary>
    public static string Normalize(string baseUrl)
    {
        var trimmed = baseUrl.Trim();
        return trimmed.Contains("://", StringComparison.Ordinal) ? trimmed : "http://" + trimmed;
    }

    /// <summary>Reads the channel's own base URL out of its decrypted config, if it set one.</summary>
    public static string? BaseUrlOf(IReadOnlyDictionary<string, string> config)
        => config.TryGetValue(BaseUrlKey, out var baseUrl) && !string.IsNullOrWhiteSpace(baseUrl)
            ? baseUrl
            : null;
}
