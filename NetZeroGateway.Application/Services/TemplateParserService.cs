using System.Text.RegularExpressions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public partial class TemplateParserService : ITemplateParserService
{
    /// <summary>
    /// Matches <c>[descriptor::content]</c>. The descriptor is captured loosely and
    /// interpreted by <see cref="ParseDescriptor"/> — a single regex covering plain tags,
    /// labelled tags and the combined form would be unreadable and hard to extend.
    /// </summary>
    [GeneratedRegex(@"\[([A-Za-z0-9_:-]+)::(.*?)\]", RegexOptions.Singleline)]
    private static partial Regex TagPattern();

    /// <summary>Slugs are lowercase alphanumeric — see ChannelConfig.LabelSlug.</summary>
    [GeneratedRegex(@"^[a-z0-9]+$")]
    private static partial Regex SlugPattern();

    /// <summary>Separates the two halves of a combined labelled send.</summary>
    private const string CombinedSeparator = "-wp_label:";

    public List<ParsedChannelJob> Parse(string messageContent)
    {
        var matches = TagPattern().Matches(messageContent);

        if (matches.Count == 0)
        {
            // No tags — backward compat: single SMS with full content
            return [new ParsedChannelJob { ChannelType = ChannelType.SMS, Message = messageContent }];
        }

        var jobs = new List<ParsedChannelJob>();

        foreach (Match m in matches)
        {
            var descriptor = m.Groups[1].Value.ToLowerInvariant();
            var content = m.Groups[2].Value.Trim();
            jobs.AddRange(ParseDescriptor(descriptor, content));
        }

        return jobs;
    }

    private static IEnumerable<ParsedChannelJob> ParseDescriptor(string descriptor, string content)
    {
        // Combined labelled form: [sms_label:x-wp_label:y::text] — one slug per leg.
        var sep = descriptor.IndexOf(CombinedSeparator, StringComparison.Ordinal);
        if (sep >= 0)
        {
            var smsPart = descriptor[..sep];
            var wpPart = descriptor[(sep + 1)..]; // keep "wp_label:y"

            var sms = SingleJob(smsPart, content);
            var wp = SingleJob(wpPart, content);

            // Half-labelled (e.g. [sms-wp_label:y::x]) is ambiguous about what the sender
            // wanted for the other leg, so reject rather than quietly defaulting it.
            if (sms is null || wp is null) yield break;

            yield return sms;
            yield return wp;
            yield break;
        }

        // Legacy combined tag: both legs go to their type's default channel.
        if (descriptor == "smswp")
        {
            yield return new ParsedChannelJob { ChannelType = ChannelType.SMS, Message = content };
            yield return new ParsedChannelJob { ChannelType = ChannelType.WP, Message = content };
            yield break;
        }

        var job = SingleJob(descriptor, content);
        if (job is not null) yield return job;
    }

    /// <summary>
    /// Maps one descriptor segment to a job. Returns null for anything unrecognised —
    /// including a malformed slug — so callers skip it rather than guess.
    /// </summary>
    private static ParsedChannelJob? SingleJob(string segment, string content)
    {
        var (name, slug) = SplitLabel(segment);
        if (slug is not null && !SlugPattern().IsMatch(slug)) return null;

        return name switch
        {
            "sms" => new ParsedChannelJob
            {
                ChannelType = ChannelType.SMS,
                Message = content,
                ChannelLabelSlug = slug
            },

            // wp carries media when the body has a ||url suffix; wpmedia is the older
            // spelling of the same thing and still resolves here.
            "wp" or "wpmedia" => BuildWhatsApp(content, slug),

            "call" => new ParsedChannelJob
            {
                ChannelType = ChannelType.Voice,
                Message = content,
                CampaignId = content,
                ChannelLabelSlug = slug
            },

            _ => null
        };
    }

    private static ParsedChannelJob BuildWhatsApp(string content, string? slug)
    {
        var sep = content.IndexOf("||", StringComparison.Ordinal);
        if (sep < 0)
        {
            return new ParsedChannelJob
            {
                ChannelType = ChannelType.WP,
                Message = content,
                ChannelLabelSlug = slug
            };
        }

        return new ParsedChannelJob
        {
            ChannelType = ChannelType.WP,
            Message = content[..sep].Trim(),
            MediaUrl = content[(sep + 2)..].Trim(),
            ChannelLabelSlug = slug
        };
    }

    /// <summary>Splits <c>sms_label:revemain</c> into ("sms", "revemain").</summary>
    private static (string Name, string? Slug) SplitLabel(string segment)
    {
        const string marker = "_label:";
        var i = segment.IndexOf(marker, StringComparison.Ordinal);
        return i < 0
            ? (segment, null)
            : (segment[..i], segment[(i + marker.Length)..]);
    }
}
