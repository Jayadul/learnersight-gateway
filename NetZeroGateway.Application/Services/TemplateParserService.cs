using System.Text.RegularExpressions;
using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Application.Services;

public partial class TemplateParserService : ITemplateParserService
{
    // Matches [tag::content] including multiline content
    [GeneratedRegex(@"\[(\w+)::(.*?)\]", RegexOptions.Singleline)]
    private static partial Regex TagPattern();

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
            var tag = m.Groups[1].Value.ToLowerInvariant();
            var content = m.Groups[2].Value.Trim();

            switch (tag)
            {
                case "sms":
                    jobs.Add(new ParsedChannelJob { ChannelType = ChannelType.SMS, Message = content });
                    break;

                case "wp":
                    jobs.Add(new ParsedChannelJob { ChannelType = ChannelType.WP_Text, Message = content });
                    break;

                case "smswp":
                    jobs.Add(new ParsedChannelJob { ChannelType = ChannelType.SMS, Message = content });
                    jobs.Add(new ParsedChannelJob { ChannelType = ChannelType.WP_Text, Message = content });
                    break;

                case "wpmedia":
                    {
                        // Split on first || → message || mediaUrl
                        var sep = content.IndexOf("||", StringComparison.Ordinal);
                        if (sep >= 0)
                        {
                            var msg = content[..sep].Trim();
                            var mediaUrl = content[(sep + 2)..].Trim();
                            jobs.Add(new ParsedChannelJob
                            {
                                ChannelType = ChannelType.WP_Media,
                                Message = msg,
                                MediaUrl = mediaUrl
                            });
                        }
                        else
                        {
                            jobs.Add(new ParsedChannelJob
                            {
                                ChannelType = ChannelType.WP_Media,
                                Message = content
                            });
                        }
                        break;
                    }

                case "call":
                    {
                        // [call::campaignId] or [call::campaignId||lotNumber].
                        // Same || convention as wpmedia; a bare campaign id keeps working
                        // and leaves the lot to the channel config.
                        var sep = content.IndexOf("||", StringComparison.Ordinal);
                        var campaignId = sep >= 0 ? content[..sep].Trim() : content;
                        var lotNumber = sep >= 0 ? content[(sep + 2)..].Trim() : null;

                        jobs.Add(new ParsedChannelJob
                        {
                            ChannelType = ChannelType.Voice,
                            Message = campaignId,
                            CampaignId = campaignId,
                            LotNumber = string.IsNullOrWhiteSpace(lotNumber) ? null : lotNumber
                        });
                        break;
                    }
            }
        }

        return jobs;
    }
}
