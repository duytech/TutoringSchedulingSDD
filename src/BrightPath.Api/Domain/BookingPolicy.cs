namespace BrightPath.Api.Domain;

/// <summary>Policy numbers the owner may still change, so they live in config (DECISIONS §1).</summary>
public sealed class BookingPolicyOptions
{
    public const string Section = "BookingPolicy";

    /// <summary>IANA zone the centre's local times are in.</summary>
    public required string TimeZone { get; init; }

    /// <summary>Tomorrow's schedule is final at this local time today.</summary>
    public required TimeOnly CutoffLocalTime { get; init; }

    /// <summary>A family cancellation closer than this to the start is chargeable.</summary>
    public required TimeSpan LateCancellationWindow { get; init; }
}

/// <summary>Time rules shared by the seed loader, create and cancel. Every instant in and out is UTC.</summary>
public sealed class BookingPolicy(BookingPolicyOptions options)
{
    private readonly TimeZoneInfo _zone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);

    /// <summary>A local date and time at the centre, as a UTC instant.</summary>
    public DateTimeOffset ToInstant(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time);
        return new DateTimeOffset(local, _zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public DateOnly LocalDate(DateTimeOffset instant) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, _zone).DateTime);

    /// <summary>
    /// True when the change is at or after the cut-off on the calendar day before the lesson,
    /// even when that day is a Monday (DECISIONS §1).
    /// </summary>
    public bool IsAfterCutoff(DateTimeOffset changedAt, DateTimeOffset sessionStartsAt) =>
        changedAt >= ToInstant(LocalDate(sessionStartsAt).AddDays(-1), options.CutoffLocalTime);

    /// <summary>Only a family cancellation inside the late window is charged (Q2).</summary>
    public bool IsChargeable(string? cancelledBy, DateTimeOffset cancelledAt, DateTimeOffset sessionStartsAt) =>
        cancelledBy == CancelledBy.Family && sessionStartsAt - cancelledAt < options.LateCancellationWindow;
}
