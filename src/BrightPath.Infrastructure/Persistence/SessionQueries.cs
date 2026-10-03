using BrightPath.Application.Sessions;
using BrightPath.Application.Tutors;
using BrightPath.Domain;

namespace BrightPath.Infrastructure.Persistence;

/// <summary>
/// The ways sessions are read, written once, so the report, the day's sessions and the create check cannot
/// load a session differently.
/// </summary>
public static class SessionQueries
{
    /// <summary>Sessions whose local start date is between from and to, both inclusive. A missing bound is open.</summary>
    public static IQueryable<Session> StartingBetween(
        this IQueryable<Session> sessions, DateOnly? from, DateOnly? to, BookingPolicy policy)
    {
        if (from is { } f)
        {
            var start = policy.ToInstant(f, TimeOnly.MinValue);
            sessions = sessions.Where(s => s.StartsAt >= start);
        }
        if (to is { } t)
        {
            var end = policy.ToInstant(t.AddDays(1), TimeOnly.MinValue);
            sessions = sessions.Where(s => s.StartsAt < end);
        }
        return sessions;
    }

    public static IQueryable<Session> StartingOn(this IQueryable<Session> sessions, DateOnly date, BookingPolicy policy) =>
        sessions.StartingBetween(date, date, policy);

    public static IQueryable<RuleSession> ToRuleSessions(this IQueryable<Session> sessions) =>
        sessions.Select(s => new RuleSession(
            s.Id,
            s.TutorId,
            s.Tutor.Name,
            s.RoomId,
            s.StartsAt,
            s.EndsAt,
            s.CancelledAt != null,
            s.Attendees
                .Select(a => new RuleAttendee(a.StudentId, a.Student.Name, a.Status, a.SourceLessonId))
                .ToList()));

    /// <summary>The changes of the sessions with these ids. BookingChange has no navigation from Session.</summary>
    public static IQueryable<BookingChange> ChangesOf(
        this IQueryable<BookingChange> all, IReadOnlyCollection<Guid> sessionIds) =>
        all.Where(c => sessionIds.Contains(c.SessionId));

    /// <summary>
    /// The sessions that moved sessions went to, as <see cref="MovedToView"/>. A target can be on another day, so it
    /// is loaded by id rather than taken from the same day's sessions. Start times are UTC.
    /// </summary>
    public static IQueryable<MovedToView> MoveTargets(this IQueryable<Session> all, IReadOnlyCollection<Guid> targetIds) =>
        all
            .Where(s => targetIds.Contains(s.Id))
            .Select(s => new MovedToView(s.Id, s.StartsAt, s.RoomId));

    /// <summary>The sessions as GetDaySessions reads them.</summary>
    public static IQueryable<GetDaySessionsResponse> ToGetDaySessionsResponses(this IQueryable<Session> sessions) =>
        sessions.Select(s => new GetDaySessionsResponse(
            s.Id,
            s.TutorId,
            s.Tutor.Name,
            s.RoomId,
            s.StartsAt,
            s.EndsAt,
            s.CancelledAt,
            s.MovedToSessionId,
            s.LegacyViolation,
            s.Attendees
                .Select(a => new GetDaySessionsResponse.Attendee(
                    a.Id, a.StudentId, a.Student.Name, a.SourceLessonId, a.Status, a.CancelledAt,
                    a.CancelledBy, a.Chargeable, a.LegacyViolation, a.Note))
                .ToList()));

    /// <summary>The sessions as GetTutorDay reads them.</summary>
    public static IQueryable<GetTutorDayResponse> ToGetTutorDayResponses(this IQueryable<Session> sessions) =>
        sessions.Select(s => new GetTutorDayResponse(
            s.Id,
            s.TutorId,
            s.Tutor.Name,
            s.RoomId,
            s.StartsAt,
            s.EndsAt,
            s.CancelledAt,
            s.MovedToSessionId,
            s.LegacyViolation,
            s.Attendees
                .Select(a => new GetTutorDayResponse.Attendee(
                    a.Id, a.StudentId, a.Student.Name, a.SourceLessonId, a.Status, a.CancelledAt,
                    a.CancelledBy, a.Chargeable, a.LegacyViolation, a.Note))
                .ToList()));

    /// <summary>The sessions as GetSession reads them.</summary>
    public static IQueryable<GetSessionResponse> ToGetSessionResponses(this IQueryable<Session> sessions) =>
        sessions.Select(s => new GetSessionResponse(
            s.Id,
            s.TutorId,
            s.Tutor.Name,
            s.RoomId,
            s.StartsAt,
            s.EndsAt,
            s.CancelledAt,
            s.MovedToSessionId,
            s.LegacyViolation,
            s.Attendees
                .Select(a => new GetSessionResponse.Attendee(
                    a.Id, a.StudentId, a.Student.Name, a.SourceLessonId, a.Status, a.CancelledAt,
                    a.CancelledBy, a.Chargeable, a.LegacyViolation, a.Note))
                .ToList()));
}
