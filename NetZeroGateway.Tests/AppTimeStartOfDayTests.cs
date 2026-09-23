using NetZeroGateway.Application.Utilities;

namespace NetZeroGateway.Tests;

public class AppTimeStartOfDayTests
{
    private static readonly TimeZoneInfo Dhaka = TimeZoneInfo.FindSystemTimeZoneById("Asia/Dhaka");

    [Fact]
    public void Early_utc_morning_is_still_the_previous_dhaka_day()
    {
        // 03:00 UTC is 09:00 in Dhaka, same calendar day: the day started 18:00 UTC before.
        var start = AppTime.StartOfDayUtc(new DateTimeOffset(2026, 9, 23, 3, 0, 0, TimeSpan.Zero), Dhaka);

        Assert.Equal(new DateTimeOffset(2026, 9, 22, 18, 0, 0, TimeSpan.Zero), start);
    }

    [Fact]
    public void Evening_utc_is_already_the_next_dhaka_day()
    {
        // 20:00 UTC on the 23rd is 02:00 on the 24th in Dhaka.
        var start = AppTime.StartOfDayUtc(new DateTimeOffset(2026, 9, 23, 20, 0, 0, TimeSpan.Zero), Dhaka);

        Assert.Equal(new DateTimeOffset(2026, 9, 23, 18, 0, 0, TimeSpan.Zero), start);
    }

    [Fact]
    public void The_result_is_a_utc_instant_safe_for_database_queries()
    {
        var start = AppTime.StartOfDayUtc(DateTimeOffset.UtcNow, Dhaka);

        Assert.Equal(TimeSpan.Zero, start.Offset);
    }
}
