using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

public class TemplateParserServiceTests
{
    private readonly TemplateParserService _parser = new();

    // ── call tag: campaignId || lotNumber ─────────────────────────────────

    [Fact]
    public void Call_tag_splits_campaign_and_lot_on_the_delimiter()
    {
        var job = Assert.Single(_parser.Parse("[call::CAMP123||LOT9]"));

        Assert.Equal(ChannelType.Voice, job.ChannelType);
        Assert.Equal("CAMP123", job.CampaignId);
        Assert.Equal("LOT9", job.LotNumber);
        // Message carries the campaign only — the delimiter must not leak through.
        Assert.Equal("CAMP123", job.Message);
    }

    [Fact]
    public void Call_tag_without_a_lot_still_works_and_leaves_lot_null()
    {
        var job = Assert.Single(_parser.Parse("[call::CAMP456]"));

        Assert.Equal("CAMP456", job.CampaignId);
        Assert.Null(job.LotNumber);
    }

    [Theory]
    [InlineData("[call::CAMP||]")]        // delimiter present, lot empty
    [InlineData("[call::CAMP||   ]")]     // delimiter present, lot whitespace
    public void Call_tag_treats_a_blank_lot_as_absent(string content)
    {
        var job = Assert.Single(_parser.Parse(content));

        Assert.Equal("CAMP", job.CampaignId);
        Assert.Null(job.LotNumber);
    }

    [Fact]
    public void Call_tag_trims_whitespace_around_both_parts()
    {
        var job = Assert.Single(_parser.Parse("[call::  CAMP7  ||  LOT7  ]"));

        Assert.Equal("CAMP7", job.CampaignId);
        Assert.Equal("LOT7", job.LotNumber);
    }

    [Fact]
    public void Call_tag_splits_on_the_first_delimiter_only()
    {
        var job = Assert.Single(_parser.Parse("[call::CAMP||LOT||EXTRA]"));

        Assert.Equal("CAMP", job.CampaignId);
        Assert.Equal("LOT||EXTRA", job.LotNumber);
    }

    // ── regression: tags that existed before 1.4 ──────────────────────────

    [Fact]
    public void Sms_tag_produces_a_single_sms_job()
    {
        var job = Assert.Single(_parser.Parse("[sms::hello]"));

        Assert.Equal(ChannelType.SMS, job.ChannelType);
        Assert.Equal("hello", job.Message);
    }

    [Fact]
    public void Wp_tag_produces_a_whatsapp_text_job()
    {
        var job = Assert.Single(_parser.Parse("[wp::hello]"));

        Assert.Equal(ChannelType.WP_Text, job.ChannelType);
    }

    [Fact]
    public void Smswp_tag_fans_out_to_both_channels()
    {
        var jobs = _parser.Parse("[smswp::hello]");

        Assert.Equal(2, jobs.Count);
        Assert.Contains(jobs, j => j.ChannelType == ChannelType.SMS);
        Assert.Contains(jobs, j => j.ChannelType == ChannelType.WP_Text);
        Assert.All(jobs, j => Assert.Equal("hello", j.Message));
    }

    [Fact]
    public void Wpmedia_tag_splits_message_from_media_url()
    {
        var job = Assert.Single(_parser.Parse("[wpmedia::caption||http://x/y.png]"));

        Assert.Equal(ChannelType.WP_Media, job.ChannelType);
        Assert.Equal("caption", job.Message);
        Assert.Equal("http://x/y.png", job.MediaUrl);
    }

    [Fact]
    public void Untagged_content_falls_back_to_a_single_plain_sms()
    {
        var job = Assert.Single(_parser.Parse("just some text"));

        Assert.Equal(ChannelType.SMS, job.ChannelType);
        Assert.Equal("just some text", job.Message);
    }

    [Fact]
    public void Multiple_tags_produce_one_job_each_in_order()
    {
        var jobs = _parser.Parse("[sms::a][call::C1||L1]");

        Assert.Equal(2, jobs.Count);
        Assert.Equal(ChannelType.SMS, jobs[0].ChannelType);
        Assert.Equal(ChannelType.Voice, jobs[1].ChannelType);
        Assert.Equal("L1", jobs[1].LotNumber);
    }

    [Fact]
    public void Non_call_tags_never_carry_a_lot_number()
    {
        var jobs = _parser.Parse("[sms::a][wp::b][wpmedia::c||u]");

        Assert.All(jobs, j => Assert.Null(j.LotNumber));
    }
}
