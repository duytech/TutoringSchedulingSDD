using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

/// <summary>
/// One day's sessions: every session that starts on the local date, cancelled ones included. The rooms and
/// tutors are reference data with endpoints of their own. Pure: no I/O, no database.
/// </summary>
public static class DaySessions
{
    public static List<SessionView> Build(
        DateOnly date,
        IEnumerable<GetDaySessionsResponse> sessions,
        IEnumerable<BookingChange> changes,
        BookingPolicy policy,
        IReadOnlyDictionary<Guid, MovedToView>? moveTargets = null)
    {
        var changesBySession = changes.ToLookup(c => c.SessionId);

        return sessions
            .Where(s => DateTimeUtils.LocalDate(policy.Zone, s.StartsAt) == date)
            .OrderBy(s => s.StartsAt)
            .ThenBy(s => s.RoomId, StringComparer.Ordinal)
            .ThenBy(s => s.Id)
            .Select(s => ToView(s, changesBySession[s.Id], policy, moveTargets))
            .ToList();
    }

    /// <summary><paramref name="moveTargets"/> holds the sessions moved-to sessions point at, with UTC start times.</summary>
    private static SessionView ToView(
        GetDaySessionsResponse s,
        IEnumerable<BookingChange> changes,
        BookingPolicy policy,
        IReadOnlyDictionary<Guid, MovedToView>? moveTargets)
    {
        var changeViews = SessionChanges.Views(changes, policy);

        return new SessionView(
            s.Id,
            s.TutorId,
            s.TutorName,
            s.RoomId,
            DateTimeUtils.ToLocal(policy.Zone, s.StartsAt),
            DateTimeUtils.ToLocal(policy.Zone, s.EndsAt),
            DateTimeUtils.ToLocal(policy.Zone, s.CancelledAt),
            s.MovedToSessionId,
            s.MovedToSessionId is { } to && moveTargets?.GetValueOrDefault(to) is { } target
                ? target with { StartsAt = DateTimeUtils.ToLocal(policy.Zone, target.StartsAt) }
                : null,
            s.LegacyViolation,
            s.Attendees
                .OrderBy(a => a.LessonId is null)
                .ThenBy(a => a.LessonId, StringComparer.Ordinal)
                .ThenBy(a => a.StudentName, StringComparer.Ordinal)
                .Select(a => new SessionAttendeeView(
                    a.Id, a.StudentId, a.StudentName, a.LessonId, a.Status,
                    DateTimeUtils.ToLocal(policy.Zone, a.CancelledAt), a.CancelledBy, a.Chargeable,
                    a.LegacyViolation, a.Note))
                .ToList(),
            changeViews);
    }
}
