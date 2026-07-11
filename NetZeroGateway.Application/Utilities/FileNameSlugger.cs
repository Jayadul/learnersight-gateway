using System.Text.RegularExpressions;

namespace NetZeroGateway.Application.Utilities;

/// <summary>
/// Converts a display file name into a URL-friendly slug, preserving the extension.
/// "Company Logo.png" → "company-logo.png"
/// </summary>
public static partial class FileNameSlugger
{
    public static string Slugify(string name, string extension)
    {
        var slug = name.Trim().ToLowerInvariant();
        slug = InvalidChars().Replace(slug, "-");
        slug = DashRuns().Replace(slug, "-").Trim('-');
        if (slug.Length == 0) slug = "file";
        return slug + extension.ToLowerInvariant();
    }

    [GeneratedRegex(@"[^a-z0-9\-]+")]
    private static partial Regex InvalidChars();

    [GeneratedRegex(@"-{2,}")]
    private static partial Regex DashRuns();
}
