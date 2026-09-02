using System.Text.RegularExpressions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public partial class TemplateParserService : ITemplateParserService
{
    /// <summary>
    /// Matches <c>[descriptor::content]</c>. The descriptor is captured loosely and
    /// interpreted by <see cref="ParseDescriptor"/> — a single regex covering both the
    /// single and the combined form would be unreadable and hard to extend.
    /// </summary>
    [GeneratedRegex(@"\[([A-Za-z0-9_:-]+)::(.*?)\]", RegexOptions.Singleline)]
    private static partial Regex TagPattern();

    /// <summary>Slugs are lowercase alphanumeric — see ChannelConfig.LabelSlug.</summary>
    [GeneratedRegex(@"^[a-z0-9]+$")]
    private static partial Regex SlugPattern();

    /// <summary>
    /// Joins the two legs of a combined send. The keyword is part of the separator on
    /// purpose: <c>sms:</c> and <c>wp:</c> are reserved and cannot appear inside a slug,
    /// so this sequence can only ever occur at the join.
    /// </summary>
    private const string CombinedSeparator = "-wp:";

    /// <summary>Separates a channel type from its label, as in <c>sms:revemain</c>.</summary>
    private const char LabelSeparator = ':';

    /// <summary>Splits a media URL from its caption, and a lot number from its campaign id.</summary>
    private const string ValueSeparator = "||";

    public ParsedTemplate Parse(string messageContent)
    {
        var matches = TagPattern().Matches(messageContent);

        if (matches.Count == 0)
        {
            // An untagged message is a plain SMS through the default SMS channel. This is
            // the only way to reach a default: every tag has to name its channel.
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
            // Step past the hyphen only — the "wp:" belongs to the right-hand leg.
            var wp = SingleJob(descriptor[(sep + 1)..], content);

            // The legs must be one SMS and one WhatsApp. Anything else — two WhatsApp
            // legs, a voice leg — is a malformed descriptor rather than a send.
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
    /// Maps one <c>type:label</c> segment to a job. Returns null for anything
    /// unrecognised — an unknown type, a malformed slug, or a missing label — so the
    /// caller can reject the whole request by name.
    /// </summary>
    private static ParsedChannelJob? SingleJob(string segment, string content)
    {
        var (name, slug) = SplitLabel(segment);

        // The label is not optional. Without it a tag would silently resolve to whichever
        // channel happens to be the default, which is exactly the ambiguity that having
        // several active channels per type introduced.
        if (slug is null || !SlugPattern().IsMatch(slug)) return null;

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

    private static ParsedChannelJob BuildWhatsApp(string content, string slug)
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

    private static ParsedChannelJob BuildVoice(string content, string slug)
    {
        // [call:slug::campaignId] or [call:slug::campaignId||lotNumber]. A bare campaign
        // id is valid and leaves the lot number to the channel config.
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

    /// <summary>
    /// Splits <c>sms:revemain</c> into ("sms", "revemain"). The slug is null when the
    /// segment carries no label at all, which is no longer a valid tag.
    /// </summary>
    private static (string Name, string? Slug) SplitLabel(string segment)
    {
        var i = segment.IndexOf(LabelSeparator);
        return i < 0
            ? (segment, null)
            : (segment[..i], segment[(i + 1)..]);
    }

    /// <summary>
    /// Names the offending tag and the form that replaces it. Release 2.1 removed the
    /// whole previous grammar, so an integrator hitting this is mid-migration rather than
    /// guessing — a specific hint is worth more than a list of everything valid.
    /// </summary>
    private static string Reject(string descriptor)
    {
        var (name, slug) = SplitLabel(descriptor);

        var hint = descriptor switch
        {
            // A voice tag carries a campaign id, not message text.
            "call" => "The channel label is now required: [call:yourlabel::your-campaign-id].",

            "sms" or "wp" =>
                $"The channel label is now required: [{descriptor}:yourlabel::your message].",

            "smswp" or "sms-wp" =>
                "Combined sends now label each leg: [sms:yourlabel-wp:yourlabel::text].",

            "wpmedia" =>
                "WhatsApp media uses the WhatsApp tag with a URL: "
                + "[wp:yourlabel::caption||https://example.com/file.pdf].",

            _ when descriptor.Contains("_label:", StringComparison.Ordinal) =>
                "The label form changed — write [sms:revemain::text], not "
                + "[sms_label:revemain::text].",

            _ when slug is not null && !SlugPattern().IsMatch(slug) =>
                $"'{slug}' is not a valid channel label. Labels are lowercase letters and "
                + "digits only, matching a channel's slug in the admin panel exactly.",

            _ when slug is not null =>
                $"'{name}' is not a channel type. Use sms, wp or call.",

            _ => "Every tag must name its channel type and label.",
        };

        return $"Unknown channel tag '{descriptor}'. {hint} "
             + "Valid tags are [sms:label::text], [wp:label::text], [wp:label::text||url], "
             + "[sms:label-wp:label::text] and [call:label::campaignId||lotNumber].";
    }
}
