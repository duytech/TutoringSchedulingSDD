using BrightPath.Common;

namespace BrightPath.Domain;

/// <summary>What the cancel check needs to know about a session. Built from the database or the seed plan.</summary>
public sealed record CancelSession(Guid Id, DateTimeOffset StartsAt, IReadOnlyList<CancelAttendee> Attendees);

public sealed record CancelAttendee(
    Guid Id, string StudentName, string Status, DateTimeOffset? CancelledAt, string? CancelledBy, string? LessonId);

/// <summary>Either the reasons a cancel is refused, or what it does.</summary>
public sealed record CancelDecision(
    IReadOnlyList<ScheduleViolation> Conflicts, bool Chargeable, bool AfterCutoff, bool CancelsSession);

/// <summary>
/// Cancelling one attendee: refused once the session has started or when the attendee is already cancelled.
/// Otherwise it is chargeable only for a late family cancel (Q2), flagged after the cut-off (Q6), and it cancels
/// the session when no one else is still booked. Pure: no I/O, no database.
/// </summary>
public static class CancelCheck
{
    public static CancelDecision Decide(
        CancelSession session, Guid attendeeId, string cancelledBy, DateTimeOffset now, BookingPolicy policy)
    {
        var attendee = session.Attendees.Single(a => a.Id == attendeeId);
        var conflicts = new List<ScheduleViolation>();

        if (now >= session.StartsAt)
        {
            conflicts.Add(Conflict(
                RuleCodes.AlreadyStarted,
                $"The session started at {DateTimeUtils.FormatLocalDateTime(policy.Zone, session.StartsAt)}, " +
                $"before now ({DateTimeUtils.FormatLocalDateTime(policy.Zone, now)})."));
        }

        if (attendee.Status == AttendeeStatus.Cancelled)
        {
            var at = attendee.CancelledAt is { } cancelledAt
                ? $" at {DateTimeUtils.FormatLocalDateTime(policy.Zone, cancelledAt)}"
                : "";
            var by = attendee.CancelledBy is { } who ? $" by {who}" : "";
            conflicts.Add(Conflict(RuleCodes.AlreadyCancelled, $"{attendee.StudentName} was already cancelled{at}{by}."));
        }

        if (conflicts.Count > 0)
        {
            return new CancelDecision(conflicts, Chargeable: false, AfterCutoff: false, CancelsSession: false);
        }

        return new CancelDecision(
            [],
            policy.IsChargeable(cancelledBy, now, session.StartsAt),
            policy.IsAfterCutoff(now, session.StartsAt),
            CancelsSession: session.Attendees.All(a => a.Id == attendeeId || a.Status != AttendeeStatus.Booked));

        ScheduleViolation Conflict(string rule, string message) =>
            new(
                rule, DateTimeUtils.LocalDate(policy.Zone, session.StartsAt), [session.Id],
                attendee.LessonId is { } l ? [l] : [], message);
    }
}
