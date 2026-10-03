namespace BrightPath.Domain;

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

    /// <summary>Counts sessions, not students: an exam pair is one (Q1).</summary>
    public required int MaxSessionsPerTutorPerDay { get; init; }

    public required int MaxAttendeesPerSession { get; init; }

    public required DayOfWeek[] ClosedDays { get; init; }

    public required TimeOnly OpensAt { get; init; }

    public required TimeOnly ClosesAt { get; init; }
}

/// <summary>
/// The centre's booking rules: its time zone, the cut-off and the late-cancellation charge. Instants are UTC.
/// </summary>
public sealed class BookingPolicy(BookingPolicyOptions options)
{
    public BookingPolicyOptions Options => options;

    /// <summary>The centre's time zone. <see cref="DateTimeUtils"/> converts to and from it.</summary>
    public TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZone);

    /// <summary>
    /// When the tutor counts as told about a lesson date: the cut-off time on the calendar day before,
    /// even when that day is a Monday (DECISIONS §1).
    /// </summary>
    public DateTimeOffset Cutoff(DateOnly lessonDate) =>
        DateTimeUtils.LocalToUtc(Zone, lessonDate.AddDays(-1), options.CutoffLocalTime);

    /// <summary>True when the change is at or after the cut-off for the lesson's date.</summary>
    public bool IsAfterCutoff(DateTimeOffset changedAt, DateTimeOffset sessionStartsAt) =>
        changedAt >= Cutoff(DateTimeUtils.LocalDate(Zone, sessionStartsAt));

    /// <summary>Only a family cancellation inside the late window is charged (Q2).</summary>
    public bool IsChargeable(string? cancelledBy, DateTimeOffset cancelledAt, DateTimeOffset sessionStartsAt) =>
        cancelledBy == CancelledBy.Family && sessionStartsAt - cancelledAt < options.LateCancellationWindow;
}
