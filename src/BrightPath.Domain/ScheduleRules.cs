using BrightPath.Common;

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
        // Step 1: keep only what holds a slot.
        var activeSessions = KeepActiveSessions(sessions);

        // Step 2: run each rule.
        var violations = new List<ScheduleViolation>();
        violations.AddRange(CheckRoomOverlaps(activeSessions, policy));
        violations.AddRange(CheckTutorOverlaps(activeSessions, policy));
        violations.AddRange(CheckStudentOverlaps(activeSessions, policy));
        violations.AddRange(CheckTutorLoad(activeSessions, policy));
        violations.AddRange(CheckClosedDays(activeSessions, policy));
        violations.AddRange(CheckOpeningHours(activeSessions, policy));
        violations.AddRange(CheckAttendeeLimit(activeSessions, policy));

        // Step 3: a stable order for the report.
        return SortViolations(violations);
    }

    /// <summary>Drops cancelled sessions and cancelled attendees, then orders by start time and first lesson id.</summary>
    private static List<RuleSession> KeepActiveSessions(IEnumerable<RuleSession> sessions)
    {
        var activeSessions = new List<RuleSession>();
        foreach (var session in sessions)
        {
            if (session.Cancelled)
            {
                continue;
            }

            var activeAttendees = session.Attendees.Where(attendee => attendee.Status != AttendeeStatus.Cancelled).ToList();
            activeSessions.Add(session with { Attendees = activeAttendees });
        }

        return activeSessions
            .OrderBy(session => session.StartsAt)
            .ThenBy(session => GetFirstLessonId(session), StringComparer.Ordinal)
            .ToList();
    }

    private static List<ScheduleViolation> SortViolations(List<ScheduleViolation> violations) =>
        violations
            .OrderBy(violation => violation.Date)
            .ThenBy(violation => Array.IndexOf(RuleCodes.All, violation.Rule))
            .ThenBy(violation => violation.LessonIds.FirstOrDefault() ?? "", StringComparer.Ordinal)
            .ThenBy(violation => violation.SessionIds[0])
            .ToList();

    private static List<ScheduleViolation> CheckRoomOverlaps(List<RuleSession> sessions, BookingPolicy policy)
    {
        var violations = new List<ScheduleViolation>();
        foreach (var (first, second) in FindOverlappingPairs(sessions, session => session.RoomId))
        {
            var message = $"{first.RoomId} holds two sessions at once: " +
                $"{first.TutorId} at {FormatStartTime(first, policy)} and {second.TutorId} at {FormatStartTime(second, policy)}.";
            violations.Add(CreateViolation(RuleCodes.RoomOverlap, [first, second], message, policy));
        }

        return violations;
    }

    private static List<ScheduleViolation> CheckTutorOverlaps(List<RuleSession> sessions, BookingPolicy policy)
    {
        var violations = new List<ScheduleViolation>();
        foreach (var (first, second) in FindOverlappingPairs(sessions, session => session.TutorId))
        {
            var message = BuildTutorOverlapMessage(first, second, policy);
            violations.Add(CreateViolation(RuleCodes.TutorOverlap, [first, second], message, policy));
        }

        return violations;
    }

    private static string BuildTutorOverlapMessage(RuleSession first, RuleSession second, BookingPolicy policy)
    {
        var tutor = $"{first.TutorId} {first.TutorName}";
        var firstStart = FormatStartTime(first, policy);
        var secondStart = FormatStartTime(second, policy);
        var sameRoom = first.RoomId == second.RoomId;
        var sameStart = HaveSameStart(first, second);

        if (sameRoom && sameStart)
        {
            return $"{tutor} has two sessions in {first.RoomId} at {firstStart}.";
        }

        if (sameRoom)
        {
            return $"{tutor} has two sessions in {first.RoomId}, at {firstStart} and {secondStart}.";
        }

        if (sameStart)
        {
            return $"{tutor} is in {first.RoomId} and {second.RoomId} at {firstStart}.";
        }

        return $"{tutor} is in {first.RoomId} at {firstStart} and in {second.RoomId} at {secondStart}.";
    }

    private static List<ScheduleViolation> CheckStudentOverlaps(List<RuleSession> sessions, BookingPolicy policy)
    {
        var violations = new List<ScheduleViolation>();

        // Step 1: for each student, the sessions they attend, in session order.
        var bookingsByStudent = sessions
            .SelectMany(session => session.Attendees.Select(attendee => (Attendee: attendee, Session: session)))
            .GroupBy(booking => booking.Attendee.StudentId);

        // Step 2: compare every two of a student's sessions.
        foreach (var studentBookings in bookingsByStudent)
        {
            var bookings = studentBookings.ToList();
            for (var i = 0; i < bookings.Count; i++)
            {
                for (var j = i + 1; j < bookings.Count; j++)
                {
                    var (firstAttendee, first) = bookings[i];
                    var (secondAttendee, second) = bookings[j];
                    if (first.Id == second.Id || !Overlaps(first, second))
                    {
                        continue;
                    }

                    var message = BuildStudentOverlapMessage(firstAttendee.StudentName, first, second, policy);
                    violations.Add(CreateViolation(
                        RuleCodes.StudentOverlap,
                        [first, second],
                        message,
                        policy,
                        lessonIds: [firstAttendee.LessonId, secondAttendee.LessonId]));
                }
            }
        }

        return violations;
    }

    private static string BuildStudentOverlapMessage(string studentName, RuleSession first, RuleSession second, BookingPolicy policy)
    {
        var firstPlace = GetRoomAndTutorId(first.RoomId, first.TutorId);
        var secondPlace = GetRoomAndTutorId(second.RoomId, second.TutorId);

        if (HaveSameStart(first, second))
        {
            return $"{studentName} is in {firstPlace} and in {secondPlace} at {FormatStartTime(first, policy)}.";
        }

        return $"{studentName} is in {firstPlace} at {FormatStartTime(first, policy)} and in {secondPlace} at {FormatStartTime(second, policy)}.";
    }

    private static List<ScheduleViolation> CheckTutorLoad(List<RuleSession> sessions, BookingPolicy policy)
    {
        var max = policy.Options.MaxSessionsPerTutorPerDay;
        var violations = new List<ScheduleViolation>();

        var sessionsByTutorAndDay = sessions.GroupBy(session => (session.TutorId, Date: DateTimeUtils.GetLocalDate(policy.Zone, session.StartsAt)));
        foreach (var tutorDay in sessionsByTutorAndDay)
        {
            var daySessions = tutorDay.ToList();
            if (daySessions.Count <= max)
            {
                continue;
            }

            var tutor = daySessions[0];
            var message = $"{tutor.TutorId} {tutor.TutorName} has {daySessions.Count} sessions on {DateTimeUtils.FormatDate(tutorDay.Key.Date)}; the limit is {max}.";
            violations.Add(CreateViolation(RuleCodes.TutorLoad, daySessions, message, policy));
        }

        return violations;
    }

    private static List<ScheduleViolation> CheckClosedDays(List<RuleSession> sessions, BookingPolicy policy)
    {
        var violations = new List<ScheduleViolation>();
        foreach (var session in sessions)
        {
            var date = DateTimeUtils.GetLocalDate(policy.Zone, session.StartsAt);
            if (!policy.Options.ClosedDays.Contains(date.DayOfWeek))
            {
                continue;
            }

            var message = $"{GetStudentNames(session.Attendees)} in {GetRoomAndTutorId(session.RoomId, session.TutorId)} " +
                $"at {FormatStartTime(session, policy)} on {date.DayOfWeek} {DateTimeUtils.FormatDate(date)}; " +
                $"the centre is closed on {date.DayOfWeek}s.";
            violations.Add(CreateViolation(RuleCodes.ClosedDay, [session], message, policy));
        }

        return violations;
    }

    private static List<ScheduleViolation> CheckOpeningHours(List<RuleSession> sessions, BookingPolicy policy)
    {
        var (opens, closes) = (policy.Options.OpensAt, policy.Options.ClosesAt);
        var violations = new List<ScheduleViolation>();
        foreach (var session in sessions)
        {
            var startTime = DateTimeUtils.GetLocalTime(policy.Zone, session.StartsAt);
            var endTime = DateTimeUtils.GetLocalTime(policy.Zone, session.EndsAt);
            var startsBeforeOpening = startTime < opens;
            var endsAfterClosing = endTime > closes;
            var endsOnAnotherDay = DateTimeUtils.GetLocalDate(policy.Zone, session.EndsAt) != DateTimeUtils.GetLocalDate(policy.Zone, session.StartsAt);
            if (!startsBeforeOpening && !endsAfterClosing && !endsOnAnotherDay)
            {
                continue;
            }

            var message = $"{GetStudentNames(session.Attendees)} in {GetRoomAndTutorId(session.RoomId, session.TutorId)} " +
                $"runs {DateTimeUtils.FormatTime(startTime)}–{DateTimeUtils.FormatTime(endTime)}, " +
                $"outside opening hours {DateTimeUtils.FormatTime(opens)}–{DateTimeUtils.FormatTime(closes)}.";
            violations.Add(CreateViolation(RuleCodes.OutsideHours, [session], message, policy));
        }

        return violations;
    }

    private static List<ScheduleViolation> CheckAttendeeLimit(List<RuleSession> sessions, BookingPolicy policy)
    {
        var max = policy.Options.MaxAttendeesPerSession;
        var violations = new List<ScheduleViolation>();
        foreach (var session in sessions)
        {
            if (session.Attendees.Count <= max)
            {
                continue;
            }

            var message = $"The session in {GetRoomAndTutorId(session.RoomId, session.TutorId)} at {FormatStartTime(session, policy)} " +
                $"has {session.Attendees.Count} attendees; the limit is {max}.";
            violations.Add(CreateViolation(RuleCodes.TooManyAttendees, [session], message, policy));
        }

        return violations;
    }

    /// <summary>The date is the first session's local date. Lesson ids default to <see cref="ListLessonIds"/>.</summary>
    private static ScheduleViolation CreateViolation(
        string rule,
        List<RuleSession> sessions,
        string message,
        BookingPolicy policy,
        IEnumerable<string?>? lessonIds = null) =>
        new(
            rule,
            DateTimeUtils.GetLocalDate(policy.Zone, sessions[0].StartsAt),
            sessions.Select(session => session.Id).ToList(),
            (lessonIds ?? ListLessonIds(sessions)).OfType<string>().ToList(),
            message);

    /// <summary>Every attendee's lesson id: session by session, sorted within each session.</summary>
    private static IEnumerable<string?> ListLessonIds(List<RuleSession> sessions) =>
        sessions.SelectMany(session => session.Attendees.Select(attendee => attendee.LessonId).Order(StringComparer.Ordinal));

    /// <summary>
    /// Every two sessions with the same key (room or tutor) that overlap. Each pair keeps the input order.
    /// </summary>
    private static List<(RuleSession First, RuleSession Second)> FindOverlappingPairs(
        List<RuleSession> sessions, Func<RuleSession, string> getKey)
    {
        var pairs = new List<(RuleSession First, RuleSession Second)>();
        foreach (var group in sessions.GroupBy(getKey))
        {
            var groupSessions = group.ToList();
            for (var i = 0; i < groupSessions.Count; i++)
            {
                for (var j = i + 1; j < groupSessions.Count; j++)
                {
                    if (Overlaps(groupSessions[i], groupSessions[j]))
                    {
                        pairs.Add((groupSessions[i], groupSessions[j]));
                    }
                }
            }
        }

        return pairs;
    }

    // Half-open [start, end), the same as the tstzrange in the exclusion constraints.
    private static bool Overlaps(RuleSession first, RuleSession second) => first.StartsAt < second.EndsAt && second.StartsAt < first.EndsAt;

    private static bool HaveSameStart(RuleSession first, RuleSession second) => first.StartsAt == second.StartsAt;

    private static string GetFirstLessonId(RuleSession session) =>
        session.Attendees.Select(attendee => attendee.LessonId).Where(lessonId => lessonId is not null).Order(StringComparer.Ordinal).FirstOrDefault() ?? "";

    /// <summary>For example "R3 with T3".</summary>
    private static string GetRoomAndTutorId(string roomId, string tutorId) => $"{roomId} with {tutorId}";

    /// <summary>For example "Le Minh Chau and Vu Ha My".</summary>
    private static string GetStudentNames(IEnumerable<RuleAttendee> attendees) => string.Join(" and ", attendees.Select(attendee => attendee.StudentName));

    /// <summary>For example "09:00", in the centre's time zone.</summary>
    private static string FormatStartTime(RuleSession session, BookingPolicy policy) =>
        DateTimeUtils.FormatTime(DateTimeUtils.GetLocalTime(policy.Zone, session.StartsAt));
}
