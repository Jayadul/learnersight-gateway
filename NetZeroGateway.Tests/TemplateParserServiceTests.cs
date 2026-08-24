using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

public class TemplateParserServiceTests
{
    private readonly TemplateParserService _parser = new();

    /// <summary>Asserts the parse succeeded and hands back the jobs.</summary>
    private List<ParsedChannelJob> ParseOk(string content)
    {
        var result = _parser.Parse(content);
        Assert.Null(result.Error);
        return result.Jobs;
    }

    // ── call tag: campaignId || lotNumber ─────────────────────────────────

    [Fact]
    public void Call_tag_splits_campaign_and_lot_on_the_delimiter()
    {
        var job = Assert.Single(ParseOk("[call::CAMP123||LOT9]"));

        Assert.Equal(ChannelType.Voice, job.ChannelType);
        Assert.Equal("CAMP123", job.CampaignId);
        Assert.Equal("LOT9", job.LotNumber);
        // Message carries the campaign only — the delimiter must not leak through.
        Assert.Equal("CAMP123", job.Message);
    }

    [Fact]
    public void Call_tag_without_a_lot_still_works_and_leaves_lot_null()
    {
        var job = Assert.Single(ParseOk("[call::CAMP456]"));

        Assert.Equal("CAMP456", job.CampaignId);
        Assert.Null(job.LotNumber);
    }

    [Theory]
    [InlineData("[call::CAMP||]")]        // delimiter present, lot empty
    [InlineData("[call::CAMP||   ]")]     // delimiter present, lot whitespace
    public void Call_tag_treats_a_blank_lot_as_absent(string content)
    {
        var job = Assert.Single(ParseOk(content));

        Assert.Equal("CAMP", job.CampaignId);
        Assert.Null(job.LotNumber);
    }

    [Fact]
    public void Call_tag_trims_whitespace_around_both_parts()
    {
        var job = Assert.Single(ParseOk("[call::  CAMP7  ||  LOT7  ]"));

        Assert.Equal("CAMP7", job.CampaignId);
        Assert.Equal("LOT7", job.LotNumber);
    }

    // ── WhatsApp: one tag, media decided by the body ──────────────────────

    [Fact]
    public void Wp_tag_without_a_url_is_a_text_send()
    {
        var job = Assert.Single(ParseOk("[wp::hello there]"));

        Assert.Equal(ChannelType.WP, job.ChannelType);
        Assert.Equal("hello there", job.Message);
        Assert.Null(job.MediaUrl);
    }

    [Fact]
    public void Wp_tag_with_a_url_is_a_media_send_on_the_same_channel()
    {
        var job = Assert.Single(ParseOk("[wp::see the invoice||https://x.test/i.pdf]"));

        Assert.Equal(ChannelType.WP, job.ChannelType);
        Assert.Equal("see the invoice", job.Message);
        Assert.Equal("https://x.test/i.pdf", job.MediaUrl);
    }

    // ── labels: any tag may name a specific channel ───────────────────────

    [Theory]
    [InlineData("[sms_label:revemain::hi]", ChannelType.SMS, "revemain")]
    [InlineData("[wp_label:watrendmain::hi]", ChannelType.WP, "watrendmain")]
    [InlineData("[call_label:voicemain::CAMP]", ChannelType.Voice, "voicemain")]
    public void A_labelled_tag_carries_its_slug(string content, ChannelType type, string slug)
    {
        var job = Assert.Single(ParseOk(content));

        Assert.Equal(type, job.ChannelType);
        Assert.Equal(slug, job.ChannelLabelSlug);
    }

    [Fact]
    public void An_unlabelled_tag_leaves_the_slug_null_so_the_default_channel_is_used()
    {
        var job = Assert.Single(ParseOk("[sms::hi]"));

        Assert.Null(job.ChannelLabelSlug);
    }

    [Theory]
    [InlineData("[sms_label:reve-main::hi]")]  // hyphen — also the leg separator
    [InlineData("[sms_label:::hi]")]           // empty slug
    public void A_malformed_slug_is_rejected_rather_than_guessed(string content)
    {
        Assert.NotNull(_parser.Parse(content).Error);
    }

    [Fact]
    public void Tags_are_case_insensitive_so_a_shouted_slug_still_resolves()
    {
        var job = Assert.Single(ParseOk("[SMS_LABEL:REVEMAIN::hi]"));

        Assert.Equal(ChannelType.SMS, job.ChannelType);
        Assert.Equal("revemain", job.ChannelLabelSlug);
    }

    // ── combined SMS + WhatsApp: either leg may be labelled ───────────────

    [Fact]
    public void Combined_tag_sends_one_sms_and_one_whatsapp()
    {
        var jobs = ParseOk("[sms-wp::big sale]");

        Assert.Equal(2, jobs.Count);
        Assert.Equal(ChannelType.SMS, jobs[0].ChannelType);
        Assert.Equal(ChannelType.WP, jobs[1].ChannelType);
        Assert.All(jobs, j => Assert.Equal("big sale", j.Message));
        Assert.All(jobs, j => Assert.Null(j.ChannelLabelSlug));
    }

    [Fact]
    public void Combined_tag_accepts_a_label_on_both_legs()
    {
        var jobs = ParseOk("[sms_label:revemain-wp_label:watrendmain::hi]");

        Assert.Equal("revemain", jobs[0].ChannelLabelSlug);
        Assert.Equal("watrendmain", jobs[1].ChannelLabelSlug);
    }

    [Fact]
    public void Combined_tag_accepts_a_label_on_the_sms_leg_only()
    {
        var jobs = ParseOk("[sms_label:revemain-wp::hi]");

        Assert.Equal("revemain", jobs[0].ChannelLabelSlug);
        // The unlabelled leg falls to its type's default channel.
        Assert.Null(jobs[1].ChannelLabelSlug);
    }

    [Fact]
    public void Combined_tag_accepts_a_label_on_the_whatsapp_leg_only()
    {
        var jobs = ParseOk("[sms-wp_label:watrendmain::hi]");

        Assert.Null(jobs[0].ChannelLabelSlug);
        Assert.Equal("watrendmain", jobs[1].ChannelLabelSlug);
    }

    [Fact]
    public void Combined_tag_carries_a_media_url_on_the_whatsapp_leg()
    {
        var jobs = ParseOk("[sms-wp::look||https://x.test/i.jpg]");

        // Both legs share the body, but only WhatsApp can deliver the media.
        Assert.Equal("look", jobs[1].Message);
        Assert.Equal("https://x.test/i.jpg", jobs[1].MediaUrl);
    }

    // ── retired spellings and unknown tags are rejected by name ───────────

    [Fact]
    public void The_retired_smswp_tag_is_rejected_and_names_its_replacement()
    {
        var result = _parser.Parse("[smswp::hi]");

        Assert.False(result.IsValid);
        Assert.Contains("smswp", result.Error);
        Assert.Contains("[sms-wp::text]", result.Error);
    }

    [Fact]
    public void The_retired_wpmedia_tag_is_rejected_and_names_its_replacement()
    {
        var result = _parser.Parse("[wpmedia::hi||https://x.test/i.jpg]");

        Assert.False(result.IsValid);
        Assert.Contains("[wp::text||url]", result.Error);
    }

    [Fact]
    public void An_unknown_tag_fails_the_whole_request_rather_than_being_dropped()
    {
        // The point of rejecting: [email::…] would otherwise vanish and the [sms::…]
        // beside it would report success for a message half of which never went.
        var result = _parser.Parse("[sms::a][email::b]");

        Assert.False(result.IsValid);
        Assert.Empty(result.Jobs);
        Assert.Contains("email", result.Error);
    }

    // ── general shape ────────────────────────────────────────────────────

    [Fact]
    public void Untagged_content_falls_back_to_a_single_plain_sms()
    {
        var job = Assert.Single(ParseOk("just some text"));

        Assert.Equal(ChannelType.SMS, job.ChannelType);
        Assert.Equal("just some text", job.Message);
    }

    [Fact]
    public void Multiple_tags_produce_one_job_each_in_order()
    {
        var jobs = ParseOk("[sms::a][call::C1||L1]");

        Assert.Equal(2, jobs.Count);
        Assert.Equal(ChannelType.SMS, jobs[0].ChannelType);
        Assert.Equal(ChannelType.Voice, jobs[1].ChannelType);
        Assert.Equal("L1", jobs[1].LotNumber);
    }

    [Fact]
    public void Non_call_tags_never_carry_a_lot_number()
    {
        var jobs = ParseOk("[sms::a][wp::b][wp::c||https://x.test/i.jpg]");

        Assert.All(jobs, j => Assert.Null(j.LotNumber));
    }
}
