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

    /// <summary>
    /// Joins the two legs of a combined send. Slugs cannot contain a hyphen, so this
    /// sequence can only occur at the join — which is what lets either leg be labelled
    /// independently: <c>sms-wp</c>, <c>sms_label:a-wp</c>, <c>sms-wp_label:b</c>,
    /// <c>sms_label:a-wp_label:b</c>.
    /// </summary>
    private const string CombinedSeparator = "-wp";

    /// <summary>Splits a media URL from its caption, and a lot number from its campaign id.</summary>
    private const string ValueSeparator = "||";

    public ParsedTemplate Parse(string messageContent)
    {
        var matches = TagPattern().Matches(messageContent);

        if (matches.Count == 0)
        {
            // No tags — backward compat: single SMS with full content
            return ParsedTemplate.Ok(
                [new ParsedChannelJob { ChannelType = ChannelType.SMS, Message = messageContent }]);
        }

        var jobs = new List<ParsedChannelJob>();

        foreach (Match m in matches)
        {
            var descriptor = m.Groups[1].Value.ToLowerInvariant();
            var content = m.Groups[2].Value.Trim();

            var parsed = ParseDescriptor(descriptor, content, out var error);
            if (error is not null) return ParsedTemplate.Invalid(error);

            jobs.AddRange(parsed);
        }

        return ParsedTemplate.Ok(jobs);
    }

    private static List<ParsedChannelJob> ParseDescriptor(
        string descriptor, string content, out string? error)
    {
        error = null;

        var sep = descriptor.IndexOf(CombinedSeparator, StringComparison.Ordinal);
        if (sep >= 0)
        {
            var sms = SingleJob(descriptor[..sep], content);
            var wp = SingleJob(descriptor[(sep + 1)..], content);

            // Either leg may carry its own label or none at all, but the legs must be one
            // SMS and one WhatsApp — anything else is a malformed descriptor, not a send.
            if (sms is null || wp is null
                || sms.ChannelType != ChannelType.SMS || wp.ChannelType != ChannelType.WP)
            {
                error = Reject(descriptor);
                return [];
            }

            return [sms, wp];
        }

        var job = SingleJob(descriptor, content);
        if (job is null)
        {
            error = Reject(descriptor);
            return [];
        }

        return [job];
    }

    /// <summary>
    /// Maps one descriptor segment to a job. Returns null for anything unrecognised —
    /// including a malformed slug — so the caller can reject the request by name.
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

            // One WhatsApp tag for both kinds of send: a body ending in ||url is a media
            // send, anything else is text. There is no separate media tag or channel.
            "wp" => BuildWhatsApp(content, slug),

            "call" => BuildVoice(content, slug),

            _ => null
        };
    }

    private static ParsedChannelJob BuildWhatsApp(string content, string? slug)
    {
        var (message, mediaUrl) = SplitValue(content);

        return new ParsedChannelJob
        {
            ChannelType = ChannelType.WP,
            Message = message,
            MediaUrl = mediaUrl,
            ChannelLabelSlug = slug
        };
    }

    private static ParsedChannelJob BuildVoice(string content, string? slug)
    {
        // [call::campaignId] or [call::campaignId||lotNumber]. A bare campaign id keeps
        // working and leaves the lot to the channel config.
        var (campaignId, lotNumber) = SplitValue(content);

        return new ParsedChannelJob
        {
            ChannelType = ChannelType.Voice,
            Message = campaignId,
            CampaignId = campaignId,
            LotNumber = lotNumber,
            ChannelLabelSlug = slug
        };
    }

    /// <summary>Splits on the first <c>||</c>; the tail is null when there is none.</summary>
    private static (string Head, string? Tail) SplitValue(string content)
    {
        var i = content.IndexOf(ValueSeparator, StringComparison.Ordinal);
        if (i < 0) return (content, null);

        var tail = content[(i + ValueSeparator.Length)..].Trim();
        return (content[..i].Trim(), string.IsNullOrWhiteSpace(tail) ? null : tail);
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

    /// <summary>
    /// Names the offending tag and its replacement. The retired spellings get a specific
    /// hint because an integrator hitting them is mid-migration, not guessing.
    /// </summary>
    private static string Reject(string descriptor)
    {
        var hint = SplitLabel(descriptor).Name switch
        {
            "smswp" => "It is now [sms-wp::text].",
            "wpmedia" => "WhatsApp media is the same tag as WhatsApp text: [wp::text||url].",
            _ => "Supported tags are [sms::text], [wp::text], [wp::text||url], "
                 + "[sms-wp::text] and [call::campaignId||lotNumber]. Any of them may name a "
                 + "channel, for example [sms_label:revemain::text].",
        };

        return $"Unknown channel tag '{descriptor}'. {hint}";
    }
}
