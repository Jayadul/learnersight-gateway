using Microsoft.Extensions.Configuration;

namespace NetZeroGateway.Application.Utilities;

/// <summary>
/// Project time standard: store and process in UTC, present in the business time zone
/// (App:TimeZone, default Asia/Dhaka). This is the ONLY place the zone is resolved —
/// never hardcode offsets elsewhere. Background jobs schedule against this zone too.
/// </summary>
public static class AppTime
{
    public const string DefaultTimeZoneId = "Asia/Dhaka";

    public static TimeZoneInfo Resolve(IConfiguration config)
        => TimeZoneInfo.FindSystemTimeZoneById(config[ConfigKeys.AppTimeZone] ?? DefaultTimeZoneId);

    /// <summary>UTC instant → business-zone wall time (for reports and human-facing output).</summary>
    public static DateTimeOffset ToAppTime(DateTimeOffset utc, TimeZoneInfo tz)
        => TimeZoneInfo.ConvertTime(utc, tz);

    /// <summary>
    /// Midnight at the start of the business-zone day containing <paramref name="utc"/>,
    /// as a UTC instant — safe to compare against stored timestamps (unlike
    /// <see cref="ToAppTime"/>, whose offset-carrying result must never reach a UTC query).
    /// </summary>
    public static DateTimeOffset StartOfDayUtc(DateTimeOffset utc, TimeZoneInfo tz)
    {
        var local = TimeZoneInfo.ConvertTime(utc, tz);
        var midnight = new DateTime(local.Year, local.Month, local.Day, 0, 0, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(midnight, tz.GetUtcOffset(midnight)).ToUniversalTime();
    }
}
