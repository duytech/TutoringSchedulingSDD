using System.Globalization;

namespace BrightPath.Api.Domain;

/// <summary>What the rules need to know about a session. Built from the database, the seed plan or a new booking.</summary>
public sealed record RuleSession(
    Guid Id,
    string TutorId,
    string TutorName,
    string RoomId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    bool Cancelled,
    IReadOnlyList<RuleAttendee> Attendees);

public sealed record RuleAttendee(Guid StudentId, string StudentName, string Status, string? LessonId);

public sealed record ScheduleViolation(
    string Rule,
    DateOnly Date,
    IReadOnlyList<Guid> SessionIds,
    IReadOnlyList<string> LessonIds,
    string Message);

/// <summary>Stable codes. The create endpoint uses them as conflict kinds in its 409.</summary>
public static class RuleCodes
{
    public const string RoomOverlap = "room-overlap";
    public const string TutorOverlap = "tutor-overlap";
    public const string StudentOverlap = "student-overlap";
    public const string TutorLoad = "tutor-load";
    public const string ClosedDay = "closed-day";
    public const string OutsideHours = "outside-hours";
    public const string TooManyAttendees = "too-many-attendees";

    /// <summary>Also the order a report lists them in, within one date.</summary>
    public static readonly string[] All =
        [RoomOverlap, TutorOverlap, StudentOverlap, TutorLoad, ClosedDay, OutsideHours, TooManyAttendees];
}

/// <summary>
/// Every centre rule, written once. The violation report runs it over the whole schedule, and the create
/// endpoint runs it over a new session plus that day's sessions, so the two can never disagree.
/// Cancelled sessions and cancelled attendees hold nothing. A no-show still holds its slot.
/// </summary>
public static class ScheduleRules
{
    public static List<ScheduleViolation> Check(IEnumerable<RuleSession> sessions, BookingPolicy policy)
    {
        var active = sessions
            .Where(s => !s.Cancelled)
            .Select(s => s with { Attendees = s.Attendees.Where(a => a.Status != AttendeeStatus.Cancelled).ToList() })
            .OrderBy(s => s.StartsAt)
            .ThenBy(s => FirstLesson(s), StringComparer.Ordinal)
            .ToList();

        var context = new Context(policy);
        List<ScheduleViolation> violations =
        [
            .. RoomOverlaps(active, context),
            .. TutorOverlaps(active, context),
            .. StudentOverlaps(active, context),
            .. TutorLoad(active, context),
            .. ClosedDays(active, context),
            .. OutsideHours(active, context),
            .. TooManyAttendees(active, context),
        ];

        return violations
            .OrderBy(v => v.Date)
        .ThenBy(v => Array.IndexOf(RuleCodes.All, v.Rule))
        .ThenBy(v => v.LessonIds.FirstOrDefault() ?? "", StringComparer.Ordinal)
        .ThenBy(v => v.SessionIds[0])
        .ToList();
    }

    private static IEnumerable<ScheduleViolation> RoomOverlaps(List<RuleSession> sessions, Context c) =>
        OverlappingPairs(sessions, s => s.RoomId).Select(p => c.Violation(
            RuleCodes.RoomOverlap,
            [p.A, p.B],
            $"{p.A.RoomId} holds two sessions at once: {p.A.TutorId} at {c.Time(p.A)} and {p.B.TutorId} at {c.Time(p.B)}."));

    private static IEnumerable<ScheduleViolation> TutorOverlaps(List<RuleSession> sessions, Context c) =>
        OverlappingPairs(sessions, s => s.TutorId).Select(p => c.Violation(
            RuleCodes.TutorOverlap,
            [p.A, p.B],
            c.SameStart(p.A, p.B)
                ? $"{Tutor(p.A)} is in {p.A.RoomId} and {p.B.RoomId} at {c.Time(p.A)}."
                : $"{Tutor(p.A)} is in {p.A.RoomId} at {c.Time(p.A)} and in {p.B.RoomId} at {c.Time(p.B)}."));

    private static IEnumerable<ScheduleViolation> StudentOverlaps(List<RuleSession> sessions, Context c) =>
        sessions
            .SelectMany(s => s.Attendees.Select(a => (Attendee: a, Session: s)))
            .GroupBy(x => x.Attendee.StudentId)
            .SelectMany(g => Pairs(g.ToList()))
            .Where(p => p.A.Session.Id != p.B.Session.Id && Overlaps(p.A.Session, p.B.Session))
            .Select(p =>
            {
                var (a, b) = (p.A.Session, p.B.Session);
                var where = c.SameStart(a, b)
                    ? $"in {Place(a)} and in {Place(b)} at {c.Time(a)}"
                    : $"in {Place(a)} at {c.Time(a)} and in {Place(b)} at {c.Time(b)}";
                return c.Violation(
                    RuleCodes.StudentOverlap,
                    [a, b],
                    $"{p.A.Attendee.StudentName} is {where}.",
                    lessonIds: [p.A.Attendee.LessonId, p.B.Attendee.LessonId]);
            });

    private static IEnumerable<ScheduleViolation> TutorLoad(List<RuleSession> sessions, Context c)
    {
        var max = c.Policy.Options.MaxSessionsPerTutorPerDay;
        return sessions
            .GroupBy(s => (s.TutorId, Date: c.Policy.LocalDate(s.StartsAt)))
            .Where(g => g.Count() > max)
            .Select(g => c.Violation(
                RuleCodes.TutorLoad,
                g.ToList(),
                $"{Tutor(g.First())} has {g.Count()} sessions on {Format(g.Key.Date)}; the limit is {max}."));
    }

    private static IEnumerable<ScheduleViolation> ClosedDays(List<RuleSession> sessions, Context c) =>
        sessions
            .Where(s => c.Policy.Options.ClosedDays.Contains(c.Policy.LocalDate(s.StartsAt).DayOfWeek))
            .Select(s =>
            {
                var date = c.Policy.LocalDate(s.StartsAt);
                return c.Violation(
                    RuleCodes.ClosedDay,
                    [s],
                    $"{Who(s)} in {Place(s)} at {c.Time(s)} on {date.DayOfWeek} {Format(date)}; the centre is closed on {date.DayOfWeek}s.");
            });

    private static IEnumerable<ScheduleViolation> OutsideHours(List<RuleSession> sessions, Context c)
    {
        var (opens, closes) = (c.Policy.Options.OpensAt, c.Policy.Options.ClosesAt);
        return sessions
            .Where(s =>
                c.Policy.LocalTime(s.StartsAt) < opens
                || c.Policy.LocalTime(s.EndsAt) > closes
                || c.Policy.LocalDate(s.EndsAt) != c.Policy.LocalDate(s.StartsAt))
            .Select(s => c.Violation(
                RuleCodes.OutsideHours,
                [s],
                $"{Who(s)} in {Place(s)} runs {c.Time(s)}–{Format(c.Policy.LocalTime(s.EndsAt))}, " +
                $"outside opening hours {Format(opens)}–{Format(closes)}."));
    }

    private static IEnumerable<ScheduleViolation> TooManyAttendees(List<RuleSession> sessions, Context c)
    {
        var max = c.Policy.Options.MaxAttendeesPerSession;
        return sessions
            .Where(s => s.Attendees.Count > max)
            .Select(s => c.Violation(
                RuleCodes.TooManyAttendees,
                [s],
                $"The session in {Place(s)} at {c.Time(s)} has {s.Attendees.Count} attendees; the limit is {max}."));
    }

    // Half-open [start, end), the same as the tstzrange in the exclusion constraints.
    private static bool Overlaps(RuleSession a, RuleSession b) => a.StartsAt < b.EndsAt && b.StartsAt < a.EndsAt;

    private static IEnumerable<(RuleSession A, RuleSession B)> OverlappingPairs(
        List<RuleSession> sessions, Func<RuleSession, string> key) =>
        sessions.GroupBy(key).SelectMany(g => Pairs(g.ToList())).Where(p => Overlaps(p.A, p.B));

    /// <summary>Every unordered pair, keeping the input order inside each pair.</summary>
    private static IEnumerable<(T A, T B)> Pairs<T>(List<T> items) =>
        items.SelectMany((a, i) => items.Skip(i + 1).Select(b => (a, b)));

    private static string FirstLesson(RuleSession s) =>
        s.Attendees.Select(a => a.LessonId).Where(id => id is not null).Order(StringComparer.Ordinal).FirstOrDefault() ?? "";

    private static string Tutor(RuleSession s) => $"{s.TutorId} {s.TutorName}";

    private static string Place(RuleSession s) => $"{s.RoomId} with {s.TutorId}";

    private static string Who(RuleSession s) => string.Join(" and ", s.Attendees.Select(a => a.StudentName));

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private sealed class Context(BookingPolicy policy)
    {
        public BookingPolicy Policy => policy;

        public string Time(RuleSession s) => Format(policy.LocalTime(s.StartsAt));

        public bool SameStart(RuleSession a, RuleSession b) => a.StartsAt == b.StartsAt;

        /// <summary>Lesson IDs default to every attendee of the given sessions, in session order.</summary>
        public ScheduleViolation Violation(
            string rule, List<RuleSession> sessions, string message, IEnumerable<string?>? lessonIds = null) =>
            new(
                rule,
                policy.LocalDate(sessions[0].StartsAt),
                sessions.Select(s => s.Id).ToList(),
                (lessonIds ?? sessions.SelectMany(s => s.Attendees.Select(a => a.LessonId).Order(StringComparer.Ordinal)))
                    .OfType<string>()
                    .ToList(),
                message);
    }
}
