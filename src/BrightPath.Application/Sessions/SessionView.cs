using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

/// <summary>One session as every read and write use case answers with it.</summary>
public sealed record SessionView(
    Guid Id,
    string TutorId,
    string TutorName,
    string RoomId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset? CancelledAt,
    Guid? MovedToSessionId,
    MovedToView? MovedTo,
    bool LegacyViolation,
    IReadOnlyList<SessionAttendeeView> Attendees,
    IReadOnlyList<SessionChangeView> Changes);

public sealed record SessionAttendeeView(
    Guid Id,
    Guid StudentId,
    string StudentName,
    string? LessonId,
    string Status,
    DateTimeOffset? CancelledAt,
    string? CancelledBy,
    bool Chargeable,
    bool LegacyViolation,
    string? Note);

public sealed record SessionChangeView(
    string Kind,
    Guid? AttendeeId,
    DateTimeOffset ChangedAt,
    string? ChangedBy,
    bool AfterCutoff,
    string? Note);

/// <summary>Where a moved session went. It may be on another day, so it is loaded by id, not from the day.</summary>
public sealed record MovedToView(Guid Id, DateTimeOffset StartsAt, string RoomId);

/// <summary>A session's booking changes as every session view shows them, in local time.</summary>
public static class SessionChanges
{
    public static List<SessionChangeView> Views(IEnumerable<BookingChange> changes, BookingPolicy policy) =>
        changes
            .OrderBy(c => c.ChangedAt)
            // The last attendee's cancel and the session's share a time: the student goes first, then the session.
            .ThenBy(c => c.AttendeeId is null)
            .Select(c => new SessionChangeView(
                c.Kind, c.AttendeeId, DateTimeUtils.ToLocal(policy.Zone, c.ChangedAt), c.ChangedBy, c.AfterCutoff,
                c.Note))
            .ToList();
}
