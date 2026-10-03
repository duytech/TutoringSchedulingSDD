using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

public sealed record DaySessionsView(
    DateOnly Date,
    DateTimeOffset Now,
    IReadOnlyList<SessionView> Sessions);

/// <summary>
/// One day's sessions: every session that starts on the local date, cancelled ones included. The rooms and
/// tutors are reference data with endpoints of their own. Pure: no I/O, no database.
/// </summary>
public static class DaySessions
{
    public static DaySessionsView Build(
        DateOnly date,
        DateTimeOffset now,
        IEnumerable<GetDaySessionsResponse> sessions,
        IEnumerable<BookingChange> changes,
        BookingPolicy policy,
        IReadOnlyDictionary<Guid, MovedToView>? moveTargets = null)
    {
        var changesBySession = changes.ToLookup(c => c.SessionId);

        var views = sessions
            .Where(s => DateTimeUtils.LocalDate(policy.Zone, s.StartsAt) == date)
            .OrderBy(s => s.StartsAt)
            .ThenBy(s => s.RoomId, StringComparer.Ordinal)
            .ThenBy(s => s.Id)
            .Select(s => ToView(s, changesBySession[s.Id], now, policy, moveTargets))
            .ToList();

        return new DaySessionsView(date, DateTimeUtils.ToLocal(policy.Zone, now), views);
    }

    /// <summary><paramref name="moveTargets"/> holds the sessions moved-to sessions point at, with UTC start times.</summary>
    private static SessionView ToView(
        GetDaySessionsResponse s,
        IEnumerable<BookingChange> changes,
        DateTimeOffset now,
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
            (int)(s.EndsAt - s.StartsAt).TotalMinutes,
            SessionState.Of(s.StartsAt, s.EndsAt, now),
            s.CancelledAt is not null,
            Local(s.CancelledAt, policy),
            s.MovedToSessionId,
            s.MovedToSessionId is { } to && moveTargets?.GetValueOrDefault(to) is { } target
                ? target with { StartsAt = DateTimeUtils.ToLocal(policy.Zone, target.StartsAt) }
                : null,
            s.LegacyViolation,
            changeViews.Any(c => c.AfterCutoff),
            s.Attendees
                .OrderBy(a => a.LessonId is null)
                .ThenBy(a => a.LessonId, StringComparer.Ordinal)
                .ThenBy(a => a.StudentName, StringComparer.Ordinal)
                .Select(a => new SessionAttendeeView(
                    a.Id, a.StudentId, a.StudentName, a.LessonId, a.Status, Local(a.CancelledAt, policy),
                    a.CancelledBy, a.Chargeable, a.LegacyViolation, a.Note))
                .ToList(),
            changeViews);
    }

    private static DateTimeOffset? Local(DateTimeOffset? instant, BookingPolicy policy) =>
        instant is { } i ? DateTimeUtils.ToLocal(policy.Zone, i) : null;
}
