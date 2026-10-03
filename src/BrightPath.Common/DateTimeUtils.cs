using System.Globalization;

namespace BrightPath.Common;

/// <summary>Conversions between UTC instants and a time zone's local dates and times, and their text forms.</summary>
public static class DateTimeUtils
{
    /// <summary>A local date and time in the zone, as a UTC instant.</summary>
    public static DateTimeOffset LocalToUtc(TimeZoneInfo zone, DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public static DateOnly LocalDate(TimeZoneInfo zone, DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    public static TimeOnly LocalTime(TimeZoneInfo zone, DateTimeOffset instant) =>
        TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    /// <summary>The instant with the zone's offset, for showing in the API (+07:00, not UTC).</summary>
    public static DateTimeOffset ToLocal(TimeZoneInfo zone, DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, zone);

    /// <summary>The instant as a local date and time in the zone: "2026-03-06 09:00".</summary>
    public static string FormatLocalDateTime(TimeZoneInfo zone, DateTimeOffset instant) =>
        ToLocal(zone, instant).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    /// <summary>"2026-03-06".</summary>
    public static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>"09:00".</summary>
    public static string FormatTime(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
