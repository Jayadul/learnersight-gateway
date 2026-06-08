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
            var tag     = m.Groups[1].Value.ToLowerInvariant();
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
                    jobs.Add(new ParsedChannelJob { ChannelType = ChannelType.SMS,     Message = content });
                    jobs.Add(new ParsedChannelJob { ChannelType = ChannelType.WP_Text, Message = content });
                    break;

                case "wpmedia":
                {
                    // Split on first || → message || mediaUrl
                    var sep = content.IndexOf("||", StringComparison.Ordinal);
                    if (sep >= 0)
                    {
                        var msg      = content[..sep].Trim();
                        var mediaUrl = content[(sep + 2)..].Trim();
                        jobs.Add(new ParsedChannelJob
                        {
                            ChannelType = ChannelType.WP_Media,
                            Message     = msg,
                            MediaUrl    = mediaUrl
                        });
                    }
                    else
                    {
                        jobs.Add(new ParsedChannelJob
                        {
                            ChannelType = ChannelType.WP_Media,
                            Message     = content
                        });
                    }
                    break;
                }

                case "call":
                    jobs.Add(new ParsedChannelJob
                    {
                        ChannelType = ChannelType.Voice,
                        Message     = content,
                        CampaignId  = content
                    });
                    break;
            }
        }

        return jobs;
    }
}
