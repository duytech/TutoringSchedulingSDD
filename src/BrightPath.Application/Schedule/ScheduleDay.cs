using BrightPath.Domain;

namespace BrightPath.Application.Schedule;

/// <summary>What the day view needs to know about a session. Built from the database or the seed plan.</summary>
public sealed record DaySession(
    Guid Id,
    string TutorId,
    string TutorName,
    string RoomId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset? CancelledAt,
    Guid? MovedToSessionId,
    bool LegacyViolation,
    IReadOnlyList<DayAttendee> Attendees);

public sealed record DayAttendee(
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

public sealed record ScheduleDayView(
    DateOnly Date,
    DateTimeOffset Now,
    IReadOnlyList<ScheduleSessionView> Sessions,
    IReadOnlyList<RoomDay> Rooms,
    IReadOnlyList<TutorDay> Tutors);

public sealed record ScheduleSessionView(
    Guid Id,
    string TutorId,
    string TutorName,
    string RoomId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int DurationMin,
    string State,
    bool Cancelled,
    DateTimeOffset? CancelledAt,
    Guid? MovedToSessionId,
    MovedToView? MovedTo,
    bool LegacyViolation,
    bool ChangedAfterCutoff,
    IReadOnlyList<ScheduleAttendeeView> Attendees,
    IReadOnlyList<ScheduleChangeView> Changes);

public sealed record ScheduleAttendeeView(
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

public sealed record ScheduleChangeView(
    string Kind,
    Guid? AttendeeId,
    DateTimeOffset ChangedAt,
    string? ChangedBy,
    bool AfterCutoff,
    string? Note);

/// <summary>Where a moved session went. It may be on another day, so it is loaded by id, not from the day.</summary>
public sealed record MovedToView(Guid Id, DateTimeOffset StartsAt, string RoomId);

public sealed record RoomDay(string Id, IReadOnlyList<Guid> SessionIds);

public sealed record TutorDay(string Id, string Name, IReadOnlyList<Guid> SessionIds);

/// <summary>Where a session is relative to now. About time only: a cancelled session still has one.</summary>
public static class SessionState
{
    public const string Past = "past";
    public const string InProgress = "in-progress";
    public const string Upcoming = "upcoming";
}

/// <summary>
/// One day's schedule: every session that starts on the local date, cancelled ones included, plus a room
/// index and a tutor index that hold session IDs only. Every room and tutor is listed, so a free room or a
/// tutor's day off shows as an empty list. Pure: no I/O, no database.
/// </summary>
public static class ScheduleDay
{
    public static ScheduleDayView Build(
        DateOnly date,
        DateTimeOffset now,
        IEnumerable<DaySession> sessions,
        IEnumerable<BookingChange> changes,
        IEnumerable<string> roomIds,
        IEnumerable<Tutor> tutors,
        BookingPolicy policy,
        IReadOnlyDictionary<Guid, MovedToView>? moveTargets = null)
    {
        var changesBySession = changes.ToLookup(c => c.SessionId);

        var views = sessions
            .Where(s => policy.LocalDate(s.StartsAt) == date)
            .OrderBy(s => s.StartsAt)
            .ThenBy(s => s.RoomId, StringComparer.Ordinal)
            .ThenBy(s => s.Id)
            .Select(s => View(s, changesBySession[s.Id], now, policy, moveTargets))
            .ToList();

        return new ScheduleDayView(
            date,
            policy.ToLocal(now),
            views,
            roomIds
                .Order(StringComparer.Ordinal)
                .Select(id => new RoomDay(id, views.Where(v => v.RoomId == id).Select(v => v.Id).ToList()))
                .ToList(),
            tutors
                .OrderBy(t => t.Id, StringComparer.Ordinal)
                .Select(t => new TutorDay(t.Id, t.Name, views.Where(v => v.TutorId == t.Id).Select(v => v.Id).ToList()))
                .ToList());
    }

    /// <summary>
    /// One session as the schedule shows it. Also the body of a create, a move and GET /api/sessions/{id}.
    /// <paramref name="moveTargets"/> holds the sessions moved-to sessions point at, with UTC start times.
    /// </summary>
    public static ScheduleSessionView View(
        DaySession s,
        IEnumerable<BookingChange> changes,
        DateTimeOffset now,
        BookingPolicy policy,
        IReadOnlyDictionary<Guid, MovedToView>? moveTargets = null)
    {
        var changeViews = changes
            .OrderBy(c => c.ChangedAt)
            // The last attendee's cancel and the session's share a time: the student goes first, then the session.
            .ThenBy(c => c.AttendeeId is null)
            .Select(c => new ScheduleChangeView(
                c.Kind, c.AttendeeId, policy.ToLocal(c.ChangedAt), c.ChangedBy, c.AfterCutoff, c.Note))
            .ToList();

        return new ScheduleSessionView(
            s.Id,
            s.TutorId,
            s.TutorName,
            s.RoomId,
            policy.ToLocal(s.StartsAt),
            policy.ToLocal(s.EndsAt),
            (int)(s.EndsAt - s.StartsAt).TotalMinutes,
            State(s, now),
            s.CancelledAt is not null,
            Local(s.CancelledAt, policy),
            s.MovedToSessionId,
            s.MovedToSessionId is { } to && moveTargets?.GetValueOrDefault(to) is { } target
                ? target with { StartsAt = policy.ToLocal(target.StartsAt) }
                : null,
            s.LegacyViolation,
            changeViews.Any(c => c.AfterCutoff),
            s.Attendees
                .OrderBy(a => a.LessonId is null)
                .ThenBy(a => a.LessonId, StringComparer.Ordinal)
                .ThenBy(a => a.StudentName, StringComparer.Ordinal)
                .Select(a => new ScheduleAttendeeView(
                    a.Id, a.StudentId, a.StudentName, a.LessonId, a.Status, Local(a.CancelledAt, policy),
                    a.CancelledBy, a.Chargeable, a.LegacyViolation, a.Note))
                .ToList(),
            changeViews);
    }

    // Half-open [start, end), like everything else: a lesson that ends at 10:00 is over at 10:00.
    private static string State(DaySession s, DateTimeOffset now) =>
        now >= s.EndsAt ? SessionState.Past
        : now >= s.StartsAt ? SessionState.InProgress
        : SessionState.Upcoming;

    private static DateTimeOffset? Local(DateTimeOffset? instant, BookingPolicy policy) =>
        instant is { } i ? policy.ToLocal(i) : null;
}
