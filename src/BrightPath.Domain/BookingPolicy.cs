using BrightPath.Common;
namespace BrightPath.Domain;

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
    public DateTimeOffset GetCutoff(DateOnly lessonDate) =>
        DateTimeUtils.ConvertLocalToUtc(Zone, lessonDate.AddDays(-1), options.CutoffLocalTime);

    /// <summary>True when the change is at or after the cut-off for the lesson's date.</summary>
    public bool IsAfterCutoff(DateTimeOffset changedAt, DateTimeOffset sessionStartsAt) =>
        changedAt >= GetCutoff(DateTimeUtils.GetLocalDate(Zone, sessionStartsAt));

    /// <summary>Only a family cancellation inside the late window is charged (Q2).</summary>
    public bool IsChargeable(string? cancelledBy, DateTimeOffset cancelledAt, DateTimeOffset sessionStartsAt) =>
        cancelledBy == CancelledBy.Family && sessionStartsAt - cancelledAt < options.LateCancellationWindow;
}
