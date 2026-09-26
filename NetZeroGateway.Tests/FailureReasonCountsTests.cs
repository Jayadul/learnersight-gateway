using NetZeroGateway.Application.Interfaces;
using NetZeroGateway.Application.Services;
using NetZeroGateway.Domain.Enums;

namespace NetZeroGateway.Tests;

/// <summary>
/// The failure-reasons chart counts failed recipient rows, the rows its drill-down lists (hotfix
/// batch 8). It used to count failed jobs: a batch of 900 was one on the chart and 900 in the modal,
/// and a failed job whose recipient had since been resent counted with no row left to show.
/// </summary>
public class FailureReasonCountsTests
{
    private static FailureReasonRow Row(string reason, ChannelType type, string label, int count)
        => new(reason, type, Guid.NewGuid(), label, count);

    [Fact]
    public void A_bar_is_the_number_of_failed_recipients_not_failed_jobs()
    {
        var result = DashboardService.BuildFailureReasons(
            [Row("Instance ID Invalidated", ChannelType.WP, "WhatsApp Text Default", 13)], 5);

        Assert.Equal(13, Assert.Single(result).Count);
    }

    [Fact]
    public void The_same_reason_on_two_channels_adds_up_and_splits_by_channel()
    {
        var result = DashboardService.BuildFailureReasons(
        [
            Row("REJECTD", ChannelType.SMS, "SMS Default", 5),
            Row("REJECTD", ChannelType.SMS, "Chithi SMS", 2),
        ], 5);

        var reason = Assert.Single(result);
        Assert.Equal(7, reason.Count);
        Assert.Equal(reason.Count, reason.ByChannel.Sum(c => c.Count));
        Assert.Equal("SMS Default", reason.ByChannel[0].ChannelLabel);
    }

    [Fact]
    public void Reasons_are_ranked_by_recipients_and_cut_to_the_top_n()
    {
        var result = DashboardService.BuildFailureReasons(
        [
            Row("a", ChannelType.SMS, "x", 1), Row("b", ChannelType.SMS, "x", 9),
            Row("c", ChannelType.SMS, "x", 4), Row("d", ChannelType.SMS, "x", 2),
        ], 2);

        Assert.Equal(["b", "c"], result.Select(r => r.Reason));
    }

    [Fact]
    public void No_failures_is_an_empty_chart()
        => Assert.Empty(DashboardService.BuildFailureReasons([], 5));
}
