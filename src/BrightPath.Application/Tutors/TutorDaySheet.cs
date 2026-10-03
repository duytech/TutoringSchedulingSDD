using BrightPath.Application.Sessions;
using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Tutors;

/// <summary>One tutor's day, read from the day's sessions, with what changed after they were told at the top.</summary>
public sealed record TutorDaySheetView(
    string TutorId,
    string TutorName,
    DateOnly Date,
    DateTimeOffset Now,
    DateTimeOffset Cutoff,
    bool Final,
    IReadOnlyList<SessionView> Sessions,
    IReadOnlyList<TutorChangeView> ChangesAfterCutoff);

/// <summary>A change after the cut-off, with enough of its session to read on its own.</summary>
public sealed record TutorChangeView(
    Guid SessionId,
    DateTimeOffset SessionStartsAt,
    string RoomId,
    string Kind,
    Guid? AttendeeId,
    string? StudentName,
    DateTimeOffset ChangedAt,
    string? ChangedBy,
    string? Note);

/// <summary>
/// The tutor day sheet (DECISIONS §2, feature 3). Each session has the shape of <see cref="SessionView"/>; a test checks the sheet and
/// <see cref="DaySessions"/> show every seeded session the same. Pure: no I/O, no database.
/// </summary>
public static class TutorDaySheet
{
    public static TutorDaySheetView Build(
        Tutor tutor,
        DateOnly date,
        DateTimeOffset now,
        IEnumerable<GetTutorDayResponse> sessions,
        IEnumerable<BookingChange> changes,
        BookingPolicy policy,
        IReadOnlyDictionary<Guid, MovedToView>? moveTargets = null)
    {
        var changesBySession = changes.ToLookup(c => c.SessionId);

        var views = sessions
            .Where(s => s.TutorId == tutor.Id && DateTimeUtils.LocalDate(policy.Zone, s.StartsAt) == date)
            .OrderBy(s => s.StartsAt)
            .ThenBy(s => s.RoomId, StringComparer.Ordinal)
            .ThenBy(s => s.Id)
            .Select(s => ToView(s, changesBySession[s.Id], policy, moveTargets))
            .ToList();

        // View has already put each session's changes in order, so a stable sort by time keeps the attendee's
        // change before the session's when they share a time.
        var late = views
            .SelectMany(s => s.Changes.Where(c => c.AfterCutoff).Select(c => new TutorChangeView(
                s.Id,
                s.StartsAt,
                s.RoomId,
                c.Kind,
                c.AttendeeId,
                s.Attendees.FirstOrDefault(a => a.Id == c.AttendeeId)?.StudentName,
                c.ChangedAt,
                c.ChangedBy,
                c.Note)))
            .OrderBy(c => c.ChangedAt)
            .ToList();

        var cutoff = policy.Cutoff(date);
        return new TutorDaySheetView(
            tutor.Id,
            tutor.Name,
            date,
            DateTimeUtils.ToLocal(policy.Zone, now),
            DateTimeUtils.ToLocal(policy.Zone, cutoff),
            now >= cutoff,
            views,
            late);
    }

    /// <summary><paramref name="moveTargets"/> holds the sessions moved-to sessions point at, with UTC start times.</summary>
    private static SessionView ToView(
        GetTutorDayResponse s,
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
