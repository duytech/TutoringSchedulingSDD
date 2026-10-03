using BrightPath.Application.Sessions;
using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Tutors;

/// <summary>One tutor's day, read from the day's sessions. Each change says whether it came after the cut-off.</summary>
public sealed record TutorDaySheetView(
    string TutorId,
    string TutorName,
    DateOnly Date,
    DateTimeOffset Now,
    DateTimeOffset Cutoff,
    bool Final,
    IReadOnlyList<SessionView> Sessions);

/// <summary>
/// The tutor day sheet (DECISIONS §2, feature 3). Each session has the shape of <see cref="SessionView"/>; a test checks the sheet and
/// <see cref="DaySessions"/> show every seeded session the same. The reader has already picked the tutor and the day.
/// Pure: no I/O, no database.
/// </summary>
public static class TutorDaySheet
{
    public static TutorDaySheetView Build(
        Tutor tutor,
        DateOnly date,
        DateTimeOffset now,
        IEnumerable<GetDaySessionsByTutorResponse> sessions,
        IEnumerable<BookingChange> changes,
        BookingPolicy policy,
        IReadOnlyDictionary<Guid, MovedToView>? moveTargets = null)
    {
        var changesBySession = changes.ToLookup(c => c.SessionId);

        var sessionViews = sessions
            .OrderBy(s => s.StartsAt)
            .ThenBy(s => s.RoomId, StringComparer.Ordinal)
            .ThenBy(s => s.Id)
            .Select(s => ToView(s, changesBySession[s.Id], policy, moveTargets))
            .ToList();

        var cutoff = policy.Cutoff(date);
        return new TutorDaySheetView(
            tutor.Id,
            tutor.Name,
            date,
            DateTimeUtils.ToLocal(policy.Zone, now),
            DateTimeUtils.ToLocal(policy.Zone, cutoff),
            now >= cutoff,
            sessionViews);
    }

    /// <summary><paramref name="moveTargets"/> holds the sessions moved-to sessions point at, with UTC start times.</summary>
    private static SessionView ToView(
        GetDaySessionsByTutorResponse s,
        IEnumerable<BookingChange> changes,
        BookingPolicy policy,
        IReadOnlyDictionary<Guid, MovedToView>? moveTargets)
    {
        var changeViews = BookingChangeViews.From(changes, policy);

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
