namespace BrightPath.Domain;

/// <summary>Conversions between UTC instants and the local dates and times of a time zone.</summary>
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
}
