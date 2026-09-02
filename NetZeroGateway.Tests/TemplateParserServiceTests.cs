using NetZeroGateway.Application.DTOs;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// The Release 2.1 grammar. Every tag names its channel by label; the only way to reach
/// a default channel is to send no tag at all. The whole previous grammar was removed,
/// so the rejection cases below are as load-bearing as the accepting ones — they are the
/// contract an integrator mid-migration actually hits.
/// </summary>
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

    /// <summary>Asserts the parse was rejected and hands back the reason.</summary>
    private string ParseRejected(string content)
    {
        var result = _parser.Parse(content);
        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
        Assert.Empty(result.Jobs);
        return result.Error!;
    }

    // ── 1. SMS ────────────────────────────────────────────────────────────

    [Fact]
    public void Sms_tag_resolves_its_label_and_carries_the_whole_body()
    {
        var job = Assert.Single(ParseOk("[sms:revemain::Your OTP is 123456]"));

        Assert.Equal(ChannelType.SMS, job.ChannelType);
        Assert.Equal("revemain", job.ChannelLabelSlug);
        Assert.Equal("Your OTP is 123456", job.Message);
        Assert.Null(job.MediaUrl);
    }

    // ── 2. WhatsApp text ──────────────────────────────────────────────────

    [Fact]
    public void Whatsapp_tag_without_a_url_is_a_text_send()
    {
        var job = Assert.Single(ParseOk("[wp:waprimary::Your appointment is confirmed.]"));

        Assert.Equal(ChannelType.WP, job.ChannelType);
        Assert.Equal("waprimary", job.ChannelLabelSlug);
        Assert.Equal("Your appointment is confirmed.", job.Message);
        Assert.Null(job.MediaUrl);
    }

    // ── 3. WhatsApp media ─────────────────────────────────────────────────

    [Fact]
    public void Whatsapp_tag_with_a_url_becomes_a_media_send_on_the_same_channel()
    {
        var job = Assert.Single(
            ParseOk("[wp:waprimary::See invoice||https://cdn.example.com/invoice.pdf]"));

        // Media is not a separate tag or channel type — the URL alone selects the sender.
        Assert.Equal(ChannelType.WP, job.ChannelType);
        Assert.Equal("waprimary", job.ChannelLabelSlug);
        Assert.Equal("See invoice", job.Message);
        Assert.Equal("https://cdn.example.com/invoice.pdf", job.MediaUrl);
    }

    [Fact]
    public void Whatsapp_url_containing_a_colon_and_slashes_survives_intact()
    {
        var job = Assert.Single(
            ParseOk("[wp:waprimary::Doc||https://a.example.com:8443/x/y.pdf?v=2]"));

        Assert.Equal("https://a.example.com:8443/x/y.pdf?v=2", job.MediaUrl);
    }

    [Fact]
    public void Whatsapp_tag_with_an_empty_url_stays_a_text_send()
    {
        var job = Assert.Single(ParseOk("[wp:waprimary::Just text||]"));

        Assert.Equal("Just text", job.Message);
        Assert.Null(job.MediaUrl);
    }

    // ── 4. Combined SMS + WhatsApp ────────────────────────────────────────

    [Fact]
    public void Combined_tag_produces_one_job_per_leg_each_with_its_own_label()
    {
        var jobs = ParseOk("[sms:revemain-wp:waprimary::Big sale this weekend!]");

        Assert.Equal(2, jobs.Count);

        var sms = jobs[0];
        Assert.Equal(ChannelType.SMS, sms.ChannelType);
        Assert.Equal("revemain", sms.ChannelLabelSlug);
        Assert.Equal("Big sale this weekend!", sms.Message);

        var wp = jobs[1];
        Assert.Equal(ChannelType.WP, wp.ChannelType);
        Assert.Equal("waprimary", wp.ChannelLabelSlug);
        Assert.Equal("Big sale this weekend!", wp.Message);
    }

    [Fact]
    public void Combined_tag_puts_the_media_url_on_the_whatsapp_leg_only()
    {
        var jobs = ParseOk("[sms:revemain-wp:waprimary::Invoice||https://cdn.example.com/i.pdf]");

        Assert.Equal(2, jobs.Count);

        // The SMS leg keeps the raw body: an SMS cannot carry media, and stripping the
        // URL would silently drop the only copy of it the recipient could act on.
        Assert.Null(jobs[0].MediaUrl);
        Assert.Equal("Invoice||https://cdn.example.com/i.pdf", jobs[0].Message);

        Assert.Equal("https://cdn.example.com/i.pdf", jobs[1].MediaUrl);
        Assert.Equal("Invoice", jobs[1].Message);
    }

    [Fact]
    public void Combined_tag_legs_may_name_different_channels_of_each_type()
    {
        var jobs = ParseOk("[sms:revebackup-wp:wasecondary::Notice]");

        Assert.Equal("revebackup", jobs[0].ChannelLabelSlug);
        Assert.Equal("wasecondary", jobs[1].ChannelLabelSlug);
    }

    [Fact]
    public void Combined_tag_rejects_two_whatsapp_legs()
    {
        ParseRejected("[wp:waprimary-wp:wasecondary::Notice]");
    }

    [Fact]
    public void Combined_tag_rejects_a_leg_that_carries_no_label()
    {
        ParseRejected("[sms-wp:waprimary::Notice]");
        ParseRejected("[sms:revemain-wp::Notice]");
    }

    // ── 5. Voice ──────────────────────────────────────────────────────────

    [Fact]
    public void Call_tag_splits_campaign_and_lot_on_the_delimiter()
    {
        var job = Assert.Single(ParseOk("[call:voicemain::due-bill-api||lot-1]"));

        Assert.Equal(ChannelType.Voice, job.ChannelType);
        Assert.Equal("voicemain", job.ChannelLabelSlug);
        Assert.Equal("due-bill-api", job.CampaignId);
        Assert.Equal("lot-1", job.LotNumber);
        // Message carries the campaign only — the delimiter must not leak through.
        Assert.Equal("due-bill-api", job.Message);
    }

    [Fact]
    public void Call_tag_without_a_lot_leaves_it_to_the_channel_config()
    {
        var job = Assert.Single(ParseOk("[call:voicemain::due-bill-api]"));

        Assert.Equal("due-bill-api", job.CampaignId);
        Assert.Null(job.LotNumber);
    }

    [Fact]
    public void Call_tag_trims_whitespace_around_both_values()
    {
        var job = Assert.Single(ParseOk("[call:voicemain::  due-bill-api  ||  lot-7  ]"));

        Assert.Equal("due-bill-api", job.CampaignId);
        Assert.Equal("lot-7", job.LotNumber);
    }

    [Fact]
    public void Call_tag_is_never_valid_as_part_of_a_combined_tag()
    {
        ParseRejected("[call:voicemain-wp:waprimary::campaign]");
    }

    // ── 6. No tag ─────────────────────────────────────────────────────────

    [Fact]
    public void An_untagged_message_is_a_plain_sms_on_the_default_channel()
    {
        var job = Assert.Single(ParseOk("Your OTP is 123456"));

        Assert.Equal(ChannelType.SMS, job.ChannelType);
        Assert.Equal("Your OTP is 123456", job.Message);
        // No label: dispatch resolves the type's default. This is the only route to one.
        Assert.Null(job.ChannelLabelSlug);
    }

    [Fact]
    public void Text_that_only_looks_like_a_tag_is_sent_as_plain_sms()
    {
        // No "::" means TagPattern never matches, so this is body text, not a bad tag.
        var job = Assert.Single(ParseOk("Meet me at [the cafe] at 5"));

        Assert.Equal(ChannelType.SMS, job.ChannelType);
        Assert.Equal("Meet me at [the cafe] at 5", job.Message);
    }

    // ── Multiple tags in one message ──────────────────────────────────────

    [Fact]
    public void Several_tags_in_one_message_each_become_a_job()
    {
        var jobs = ParseOk(
            "[sms:revemain::Text part] [wp:waprimary::Chat part] [call:voicemain::camp||lot-2]");

        Assert.Equal(3, jobs.Count);
        Assert.Equal(ChannelType.SMS, jobs[0].ChannelType);
        Assert.Equal(ChannelType.WP, jobs[1].ChannelType);
        Assert.Equal(ChannelType.Voice, jobs[2].ChannelType);
    }

    [Fact]
    public void One_bad_tag_rejects_the_whole_message_even_beside_good_ones()
    {
        // Partial acceptance would deliver half a message and report success for all of it.
        ParseRejected("[sms:revemain::Fine] [nope:x::Broken]");
    }

    // ── Labels ────────────────────────────────────────────────────────────

    [Fact]
    public void Tags_are_case_insensitive_so_a_shouted_slug_still_resolves()
    {
        var job = Assert.Single(ParseOk("[SMS:REVEMAIN::Hi]"));

        Assert.Equal(ChannelType.SMS, job.ChannelType);
        Assert.Equal("revemain", job.ChannelLabelSlug);
    }

    [Fact]
    public void A_numeric_label_is_valid()
    {
        var job = Assert.Single(ParseOk("[sms:reve2::Hi]"));

        Assert.Equal("reve2", job.ChannelLabelSlug);
    }

    [Fact]
    public void A_label_with_a_hyphen_is_rejected_because_it_would_split_a_combined_tag()
    {
        var error = ParseRejected("[sms:reve-main::Hi]");

        Assert.Contains("lowercase letters and digits", error);
    }

    [Fact]
    public void A_label_with_an_underscore_is_rejected()
    {
        ParseRejected("[sms:reve_main::Hi]");
    }

    [Fact]
    public void An_empty_label_is_rejected()
    {
        ParseRejected("[sms:::Hi]");
    }

    [Fact]
    public void An_unknown_channel_type_names_the_type_in_the_reason()
    {
        var error = ParseRejected("[email:main::Hi]");

        Assert.Contains("'email' is not a channel type", error);
    }

    // ── Removed Release 2 grammar ─────────────────────────────────────────
    // These all worked in 2.0 and are gone. Each rejection names its replacement,
    // because anyone hitting one is mid-migration rather than guessing.

    [Fact]
    public void The_bare_sms_tag_is_gone_and_the_reason_asks_for_a_label()
    {
        var error = ParseRejected("[sms::Your OTP is 123456]");

        Assert.Contains("label is now required", error);
        Assert.Contains("[sms:yourlabel::", error);
    }

    [Fact]
    public void The_bare_whatsapp_tag_is_gone()
    {
        var error = ParseRejected("[wp::Hello]");

        Assert.Contains("label is now required", error);
    }

    [Fact]
    public void The_bare_call_tag_is_gone_and_its_hint_names_a_campaign_not_a_message()
    {
        var error = ParseRejected("[call::CAMP123||LOT9]");

        Assert.Contains("label is now required", error);
        // A voice tag carries a campaign id; telling the caller to put "your message"
        // there would document a payload the provider never reads.
        Assert.Contains("[call:yourlabel::your-campaign-id]", error);
    }

    [Fact]
    public void The_old_underscore_label_form_is_gone_and_points_at_the_new_one()
    {
        var error = ParseRejected("[sms_label:revemain::Hi]");

        Assert.Contains("label form changed", error);
    }

    [Fact]
    public void The_old_combined_label_form_is_gone()
    {
        ParseRejected("[sms_label:revemain-wp_label:waprimary::Hi]");
    }

    [Fact]
    public void The_unlabelled_combined_tag_is_gone_and_points_at_the_new_one()
    {
        var error = ParseRejected("[sms-wp::Big sale!]");

        Assert.Contains("label each leg", error);
    }

    [Fact]
    public void The_retired_smswp_spelling_still_gets_its_own_hint()
    {
        var error = ParseRejected("[smswp::Big sale!]");

        Assert.Contains("label each leg", error);
    }

    [Fact]
    public void The_retired_wpmedia_tag_points_at_the_url_form()
    {
        var error = ParseRejected("[wpmedia::Invoice||https://cdn.example.com/i.pdf]");

        Assert.Contains("WhatsApp tag with a URL", error);
    }

    [Fact]
    public void Every_rejection_lists_the_valid_forms()
    {
        // Whatever the specific hint, the reason always ends with the full grammar, so a
        // caller who has never seen the docs can recover from the error alone.
        foreach (var bad in new[] { "[sms::x]", "[wpmedia::x]", "[email:main::x]", "[sms:::x]" })
            Assert.Contains("[sms:label-wp:label::text]", ParseRejected(bad));
    }
}
