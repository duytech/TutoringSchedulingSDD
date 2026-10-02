using System.Globalization;

namespace BrightPath.Domain;

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

    /// <summary>
    /// Create only, so not in <see cref="All"/>: history is always in the past, and the report must not flag it.
    /// </summary>
    public const string InThePast = "in-the-past";

    /// <summary>Cancel only: a session that has started is a lesson or a no-show, not a cancellation.</summary>
    public const string AlreadyStarted = "already-started";

    /// <summary>Cancel only.</summary>
    public const string AlreadyCancelled = "already-cancelled";
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

        List<ScheduleViolation> violations =
        [
            .. RoomOverlaps(active, policy),
            .. TutorOverlaps(active, policy),
            .. StudentOverlaps(active, policy),
            .. TutorLoad(active, policy),
            .. ClosedDays(active, policy),
            .. OutsideHours(active, policy),
            .. TooManyAttendees(active, policy),
        ];

        return violations
            .OrderBy(v => v.Date)
        .ThenBy(v => Array.IndexOf(RuleCodes.All, v.Rule))
        .ThenBy(v => v.LessonIds.FirstOrDefault() ?? "", StringComparer.Ordinal)
        .ThenBy(v => v.SessionIds[0])
        .ToList();
    }

    private static IEnumerable<ScheduleViolation> RoomOverlaps(List<RuleSession> sessions, BookingPolicy policy) =>
        OverlappingPairs(sessions, s => s.RoomId).Select(p => Violation(
            RuleCodes.RoomOverlap,
            [p.A, p.B],
            $"{p.A.RoomId} holds two sessions at once: {p.A.TutorId} at {GetLocalStartTime(p.A, policy)} and {p.B.TutorId} at {GetLocalStartTime(p.B, policy)}.",
            policy));

    private static IEnumerable<ScheduleViolation> TutorOverlaps(List<RuleSession> sessions, BookingPolicy policy) =>
        OverlappingPairs(sessions, s => s.TutorId).Select(p => Violation(
            RuleCodes.TutorOverlap,
            [p.A, p.B],
            (p.A.RoomId == p.B.RoomId, SameStart(p.A, p.B)) switch
            {
                (true, true) => $"{p.A.TutorId} {p.A.TutorName} has two sessions in {p.A.RoomId} at {GetLocalStartTime(p.A, policy)}.",
                (true, false) => $"{p.A.TutorId} {p.A.TutorName} has two sessions in {p.A.RoomId}, at {GetLocalStartTime(p.A, policy)} and {GetLocalStartTime(p.B, policy)}.",
                (false, true) => $"{p.A.TutorId} {p.A.TutorName} is in {p.A.RoomId} and {p.B.RoomId} at {GetLocalStartTime(p.A, policy)}.",
                (false, false) => $"{p.A.TutorId} {p.A.TutorName} is in {p.A.RoomId} at {GetLocalStartTime(p.A, policy)} and in {p.B.RoomId} at {GetLocalStartTime(p.B, policy)}.",
            },
            policy));

    private static IEnumerable<ScheduleViolation> StudentOverlaps(List<RuleSession> sessions, BookingPolicy policy) =>
        sessions
            .SelectMany(s => s.Attendees.Select(a => (Attendee: a, Session: s)))
            .GroupBy(x => x.Attendee.StudentId)
            .SelectMany(g => Pairs(g.ToList()))
            .Where(p => p.A.Session.Id != p.B.Session.Id && Overlaps(p.A.Session, p.B.Session))
            .Select(p =>
            {
                var (a, b) = (p.A.Session, p.B.Session);
                var where = SameStart(a, b)
                    ? $"in {GetRoomAndTutorId(a.RoomId, a.TutorId)} and in {GetRoomAndTutorId(b.RoomId, b.TutorId)} at {GetLocalStartTime(a, policy)}"
                    : $"in {GetRoomAndTutorId(a.RoomId, a.TutorId)} at {GetLocalStartTime(a, policy)} and in {GetRoomAndTutorId(b.RoomId, b.TutorId)} at {GetLocalStartTime(b, policy)}";
                return Violation(
                    RuleCodes.StudentOverlap,
                    [a, b],
                    $"{p.A.Attendee.StudentName} is {where}.",
                    policy,
                    lessonIds: [p.A.Attendee.LessonId, p.B.Attendee.LessonId]);
            });

    private static IEnumerable<ScheduleViolation> TutorLoad(List<RuleSession> sessions, BookingPolicy policy)
    {
        var max = policy.Options.MaxSessionsPerTutorPerDay;
        return sessions
            .GroupBy(s => (s.TutorId, Date: policy.LocalDate(s.StartsAt)))
            .Where(g => g.Count() > max)
            .Select(g => Violation(
                RuleCodes.TutorLoad,
                g.ToList(),
                $"{g.First().TutorId} {g.First().TutorName} has {g.Count()} sessions on {IsoDate(g.Key.Date)}; the limit is {max}.",
                policy));
    }

    private static IEnumerable<ScheduleViolation> ClosedDays(List<RuleSession> sessions, BookingPolicy policy) =>
        sessions
            .Where(s => policy.Options.ClosedDays.Contains(policy.LocalDate(s.StartsAt).DayOfWeek))
            .Select(s =>
            {
                var date = policy.LocalDate(s.StartsAt);
                return Violation(
                    RuleCodes.ClosedDay,
                    [s],
                    $"{GetStudentNames(s.Attendees)} in {GetRoomAndTutorId(s.RoomId, s.TutorId)} at {GetLocalStartTime(s, policy)} on {date.DayOfWeek} {IsoDate(date)}; the centre is closed on {date.DayOfWeek}s.",
                    policy);
            });

    private static IEnumerable<ScheduleViolation> OutsideHours(List<RuleSession> sessions, BookingPolicy policy)
    {
        var (opens, closes) = (policy.Options.OpensAt, policy.Options.ClosesAt);
        return sessions
            .Where(s =>
                policy.LocalTime(s.StartsAt) < opens
                || policy.LocalTime(s.EndsAt) > closes
                || policy.LocalDate(s.EndsAt) != policy.LocalDate(s.StartsAt))
            .Select(s => Violation(
                RuleCodes.OutsideHours,
                [s],
                $"{GetStudentNames(s.Attendees)} in {GetRoomAndTutorId(s.RoomId, s.TutorId)} runs {GetLocalStartTime(s, policy)}–{HourMinute(policy.LocalTime(s.EndsAt))}, " +
                $"outside opening hours {HourMinute(opens)}–{HourMinute(closes)}.",
                policy));
    }

    private static IEnumerable<ScheduleViolation> TooManyAttendees(List<RuleSession> sessions, BookingPolicy policy)
    {
        var max = policy.Options.MaxAttendeesPerSession;
        return sessions
            .Where(s => s.Attendees.Count > max)
            .Select(s => Violation(
                RuleCodes.TooManyAttendees,
                [s],
                $"The session in {GetRoomAndTutorId(s.RoomId, s.TutorId)} at {GetLocalStartTime(s, policy)} has {s.Attendees.Count} attendees; the limit is {max}.",
                policy));
    }

    /// <summary>Lesson IDs default to every attendee of the given sessions, in session order.</summary>
    private static ScheduleViolation Violation(
        string rule,
        List<RuleSession> sessions,
        string message,
        BookingPolicy policy,
        IEnumerable<string?>? lessonIds = null) =>
        new(
            rule,
            policy.LocalDate(sessions[0].StartsAt),
            sessions.Select(s => s.Id).ToList(),
            (lessonIds ?? sessions.SelectMany(s => s.Attendees.Select(a => a.LessonId).Order(StringComparer.Ordinal)))
                .OfType<string>()
                .ToList(),
            message);

    // Half-open [start, end), the same as the tstzrange in the exclusion constraints.
    private static bool Overlaps(RuleSession a, RuleSession b) => a.StartsAt < b.EndsAt && b.StartsAt < a.EndsAt;

    private static bool SameStart(RuleSession a, RuleSession b) => a.StartsAt == b.StartsAt;

    private static IEnumerable<(RuleSession A, RuleSession B)> OverlappingPairs(
        List<RuleSession> sessions, Func<RuleSession, string> key) =>
        sessions.GroupBy(key).SelectMany(g => Pairs(g.ToList())).Where(p => Overlaps(p.A, p.B));

    /// <summary>Every unordered pair, keeping the input order inside each pair.</summary>
    private static IEnumerable<(T A, T B)> Pairs<T>(List<T> items) =>
        items.SelectMany((a, i) => items.Skip(i + 1).Select(b => (a, b)));

    private static string FirstLesson(RuleSession s) =>
        s.Attendees.Select(a => a.LessonId).Where(id => id is not null).Order(StringComparer.Ordinal).FirstOrDefault() ?? "";

    /// <summary>For example "R3 with T3".</summary>
    private static string GetRoomAndTutorId(string roomId, string tutorId) => $"{roomId} with {tutorId}";

    /// <summary>For example "Le Minh Chau and Vu Ha My".</summary>
    private static string GetStudentNames(IEnumerable<RuleAttendee> attendees) => string.Join(" and ", attendees.Select(a => a.StudentName));

    private static string IsoDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string HourMinute(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string GetLocalStartTime(RuleSession s, BookingPolicy policy) => HourMinute(policy.LocalTime(s.StartsAt));
}
