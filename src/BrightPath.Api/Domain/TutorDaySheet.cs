namespace BrightPath.Api.Domain;

/// <summary>One tutor's day, read from the schedule, with what changed after they were told at the top.</summary>
public sealed record TutorDaySheetView(
    string TutorId,
    string TutorName,
    DateOnly Date,
    DateTimeOffset Now,
    DateTimeOffset Cutoff,
    bool Final,
    IReadOnlyList<ScheduleSessionView> Sessions,
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
/// The tutor day sheet (DECISIONS §2, feature 3). Each session is <see cref="ScheduleDay.View"/>, so the sheet
/// and the schedule cannot show a session differently. Pure: no I/O, no database.
/// </summary>
public static class TutorDaySheet
{
    public static TutorDaySheetView Build(
        Tutor tutor,
        DateOnly date,
        DateTimeOffset now,
        IEnumerable<DaySession> sessions,
        IEnumerable<BookingChange> changes,
        BookingPolicy policy,
        IReadOnlyDictionary<Guid, MovedToView>? moveTargets = null)
    {
        var changesBySession = changes.ToLookup(c => c.SessionId);

        var views = sessions
            .Where(s => s.TutorId == tutor.Id && policy.LocalDate(s.StartsAt) == date)
            .OrderBy(s => s.StartsAt)
            .ThenBy(s => s.RoomId, StringComparer.Ordinal)
            .ThenBy(s => s.Id)
            .Select(s => ScheduleDay.View(s, changesBySession[s.Id], now, policy, moveTargets))
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
            tutor.Id, tutor.Name, date, policy.ToLocal(now), policy.ToLocal(cutoff), now >= cutoff, views, late);
    }
}
