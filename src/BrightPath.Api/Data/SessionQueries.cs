using BrightPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api.Data;

/// <summary>
/// The ways sessions are read, written once, so the report, the schedule and the create check cannot
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

    /// <summary>
    /// Where the moved ones among <paramref name="sessions"/> went, by id. A target can be on another day, so it is
    /// loaded by id rather than taken from the same day's sessions. Start times are UTC.
    /// </summary>
    public static async Task<Dictionary<Guid, MovedToView>> MoveTargetsAsync(
        this IQueryable<Session> all, IEnumerable<DaySession> sessions, CancellationToken ct)
    {
        var targetIds = sessions.Select(s => s.MovedToSessionId).OfType<Guid>().ToList();
        if (targetIds.Count == 0)
        {
            return [];
        }

        return await all
            .Where(s => targetIds.Contains(s.Id))
            .Select(s => new MovedToView(s.Id, s.StartsAt, s.RoomId))
            .ToDictionaryAsync(t => t.Id, ct);
    }

    public static IQueryable<DaySession> ToDaySessions(this IQueryable<Session> sessions) =>
        sessions.Select(s => new DaySession(
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
                .Select(a => new DayAttendee(
                    a.Id, a.StudentId, a.Student.Name, a.SourceLessonId, a.Status, a.CancelledAt,
                    a.CancelledBy, a.Chargeable, a.LegacyViolation, a.Note))
                .ToList()));
}
