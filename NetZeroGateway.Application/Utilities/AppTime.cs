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
}
