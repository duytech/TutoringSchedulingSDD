using BrightPath.Common;
using BrightPath.Domain;
using BrightPath.Infrastructure.Seed;

namespace BrightPath.Api.UnitTests;

public sealed class ScheduleRulesTests
{
    private static readonly BookingPolicy Policy = TestPolicy.Create();

    [Fact]
    public void Real_export_breaks_exactly_four_rules()
    {
        var plan = SeedPlanner.Plan(
            SeedCsv.ReadTutors(Path.Combine(SeedCsv.Directory, "tutors.csv")),
            SeedCsv.ReadLessons(Path.Combine(SeedCsv.Directory, "lessons_export.csv")),
            Policy);
        var tutors = plan.Tutors.ToDictionary(t => t.Id, t => t.Name);
        var students = plan.Students.ToDictionary(s => s.Id, s => s.Name);
        var sessions = plan.Sessions.Select(s => new RuleSession(
            s.Id, s.TutorId, tutors[s.TutorId], s.RoomId, s.StartsAt, s.EndsAt, s.CancelledAt is not null,
            s.Attendees.Select(a => new RuleAttendee(a.StudentId, students[a.StudentId], a.Status, a.SourceLessonId)).ToList()));

        var violations = ScheduleRules.Check(sessions, Policy);

        Assert.Collection(
            violations,
            v => AssertViolation(v, RuleCodes.StudentOverlap, "2026-03-04", ["L007", "L008"], sessions: 2),
            v => AssertViolation(
                v, RuleCodes.TutorLoad, "2026-03-06", ["L018", "L021", "L022", "L024", "L025", "L026", "L027"], sessions: 7),
            v => AssertViolation(v, RuleCodes.ClosedDay, "2026-03-09", ["L032"], sessions: 1),
            v => AssertViolation(v, RuleCodes.TutorOverlap, "2026-03-10", ["L033", "L034"], sessions: 2));

        Assert.Equal("Le Minh Chau is in R3 with T3 and in R2 with T2 at 09:00.", violations[0].Message);
        Assert.Equal("T1 Ngoc Anh is in R1 and R2 at 09:00.", violations[3].Message);
    }

    [Fact]
    public void Two_sessions_in_one_room_at_once_break_room_overlap()
    {
        var violation = Assert.Single(ScheduleRules.Check(
            [Session("T1", "R1", "09:00", 60), Session("T2", "R1", "09:30", 60)], Policy));
        Assert.Equal(RuleCodes.RoomOverlap, violation.Rule);
        Assert.Equal("R1 holds two sessions at once: T1 at 09:00 and T2 at 09:30.", violation.Message);
    }

    [Theory]
    [InlineData("R1", "09:00", "T1 Ngoc Anh has two sessions in R1 at 09:00.")]
    [InlineData("R1", "09:30", "T1 Ngoc Anh has two sessions in R1, at 09:00 and 09:30.")]
    [InlineData("R2", "09:00", "T1 Ngoc Anh is in R1 and R2 at 09:00.")]
    [InlineData("R2", "09:30", "T1 Ngoc Anh is in R1 at 09:00 and in R2 at 09:30.")]
    public void Tutor_overlap_message_names_rooms_and_times(string secondRoom, string secondStart, string message)
    {
        var first = Session("T1", "R1", "09:00", 60) with { TutorName = "Ngoc Anh" };
        var second = Session("T1", secondRoom, secondStart, 60) with { TutorName = "Ngoc Anh" };

        var violations = ScheduleRules.Check([first, second], Policy);

        var tutorOverlap = Assert.Single(violations, violation => violation.Rule == RuleCodes.TutorOverlap);
        Assert.Equal(message, tutorOverlap.Message);
    }

    [Fact]
    public void Student_overlap_names_both_sessions_and_their_lessons()
    {
        var student = Booked("A");
        var first = Session("T1", "R1", "09:00", 60, student with { LessonId = "L1" });
        var second = Session("T2", "R2", "09:30", 60, student with { LessonId = "L2" });

        var violation = Assert.Single(ScheduleRules.Check([first, second], Policy));

        Assert.Equal(RuleCodes.StudentOverlap, violation.Rule);
        Assert.Equal("A is in R1 with T1 at 09:00 and in R2 with T2 at 09:30.", violation.Message);
        Assert.Equal(["L1", "L2"], violation.LessonIds);
    }

    [Fact]
    public void Touching_sessions_do_not_overlap()
    {
        Assert.Empty(ScheduleRules.Check(
            [Session("T1", "R1", "09:00", 60), Session("T1", "R1", "10:00", 60)], Policy));
    }

    [Theory]
    [InlineData("08:30", 60, true)] // starts before opening
    [InlineData("09:00", 60, false)] // starts exactly at opening
    [InlineData("20:00", 90, false)] // ends exactly at 21:30
    [InlineData("20:30", 90, true)] // ends at 22:00
    [InlineData("21:00", 240, true)] // ends at 01:00 the next day
    public void Session_must_fit_opening_hours(string start, int minutes, bool broken)
    {
        var violations = ScheduleRules.Check([Session("T1", "R1", start, minutes)], Policy);
        Assert.Equal(broken, violations.Any(v => v.Rule == RuleCodes.OutsideHours));
    }

    [Fact]
    public void Outside_hours_message_shows_the_session_and_opening_hours()
    {
        var violation = Assert.Single(ScheduleRules.Check([Session("T1", "R1", "08:30", 60, Booked("A"))], Policy));

        Assert.Equal(RuleCodes.OutsideHours, violation.Rule);
        Assert.Equal("A in R1 with T1 runs 08:30–09:30, outside opening hours 09:00–21:30.", violation.Message);
    }

    [Fact]
    public void Three_attendees_are_too_many_but_a_cancelled_one_does_not_count()
    {
        var three = Session("T1", "R1", "11:00", 90, Booked("A"), Booked("B"), Booked("C"));
        var violation = Assert.Single(ScheduleRules.Check([three], Policy));
        Assert.Equal(RuleCodes.TooManyAttendees, violation.Rule);
        Assert.Equal("The session in R1 with T1 at 11:00 has 3 attendees; the limit is 2.", violation.Message);

        var twoAndCancelled = Session("T1", "R1", "11:00", 90, Booked("A"), Booked("B"), Cancelled("C"));
        Assert.Empty(ScheduleRules.Check([twoAndCancelled], Policy));
    }

    [Fact]
    public void Seven_sessions_in_a_day_break_tutor_load()
    {
        var violation = Assert.Single(ScheduleRules.Check(TutorDay(7), Policy));
        Assert.Equal(RuleCodes.TutorLoad, violation.Rule);
        Assert.Equal(7, violation.SessionIds.Count);
        Assert.Equal("T1 T1 has 7 sessions on 2026-03-06; the limit is 6.", violation.Message);
    }

    [Fact]
    public void Seven_sessions_over_two_days_are_within_the_load()
    {
        var sessions = TutorDay(7);
        var lastSession = sessions[6];
        sessions[6] = lastSession with { StartsAt = lastSession.StartsAt.AddDays(1), EndsAt = lastSession.EndsAt.AddDays(1) };
        Assert.Empty(ScheduleRules.Check(sessions, Policy));
    }

    [Fact]
    public void Six_sessions_and_a_cancelled_one_are_within_the_load()
    {
        var sessions = TutorDay(7);
        sessions[6] = sessions[6] with { Cancelled = true };
        Assert.Empty(ScheduleRules.Check(sessions, Policy));
    }

    [Fact]
    public void Exam_pair_counts_as_one_session_toward_the_load()
    {
        var sessions = TutorDay(6);
        sessions[0] = sessions[0] with { Attendees = [Booked("A"), Booked("B")] };
        Assert.Empty(ScheduleRules.Check(sessions, Policy));
    }

    [Fact]
    public void A_no_show_still_holds_the_students_slot()
    {
        var student = Booked("A");
        var violation = Assert.Single(ScheduleRules.Check(
            [
                Session("T1", "R1", "09:00", 60, student with { Status = AttendeeStatus.NoShow }),
                Session("T2", "R2", "09:30", 60, student),
            ],
            Policy));
        Assert.Equal(RuleCodes.StudentOverlap, violation.Rule);
    }

    [Fact]
    public void A_cancelled_attendee_frees_the_students_slot()
    {
        var student = Booked("A");
        Assert.Empty(ScheduleRules.Check(
            [
                Session("T1", "R1", "09:00", 60, student with { Status = AttendeeStatus.Cancelled }),
                Session("T2", "R2", "09:30", 60, student),
            ],
            Policy));
    }

    [Fact]
    public void A_cancelled_session_breaks_nothing()
    {
        // On a Monday, before opening, with three attendees, and overlapping another cancelled session.
        var broken = OnMonday(Session("T1", "R1", "08:00", 60, Booked("A"), Booked("B"), Booked("C")));
        Assert.Empty(ScheduleRules.Check(
            [broken with { Cancelled = true }, broken with { Id = Guid.NewGuid(), Cancelled = true }], Policy));
    }

    [Fact]
    public void A_monday_session_breaks_closed_day()
    {
        var violation = Assert.Single(ScheduleRules.Check([OnMonday(Session("T3", "R3", "10:00", 60))], Policy));
        Assert.Equal(RuleCodes.ClosedDay, violation.Rule);
    }

    [Fact]
    public void Closed_day_message_names_the_day()
    {
        var violation = Assert.Single(ScheduleRules.Check([OnMonday(Session("T3", "R3", "10:00", 60, Booked("A")))], Policy));
        Assert.Equal("A in R3 with T3 at 10:00 on Monday 2026-03-09; the centre is closed on Mondays.", violation.Message);
    }

    [Fact]
    public void Violations_are_ordered_by_date_then_rule()
    {
        // Monday: closed, before opening and three attendees. Friday: two sessions in one room.
        var monday = OnMonday(Session("T3", "R3", "08:00", 60, Booked("A"), Booked("B"), Booked("C")));
        var fridayFirst = Session("T1", "R1", "09:00", 60);
        var fridaySecond = Session("T2", "R1", "09:30", 60);

        var violations = ScheduleRules.Check([monday, fridayFirst, fridaySecond], Policy);

        var rules = violations.Select(violation => violation.Rule);
        Assert.Equal(
            [RuleCodes.RoomOverlap, RuleCodes.ClosedDay, RuleCodes.OutsideHours, RuleCodes.TooManyAttendees],
            rules);
    }

    [Fact]
    public void Lesson_ids_are_sorted_and_skip_missing_and_cancelled_attendees()
    {
        var session = Session(
            "T1", "R1", "11:00", 90,
            Booked("A") with { LessonId = "L2" },
            Booked("B") with { LessonId = "L1" },
            Booked("C"),
            Cancelled("D") with { LessonId = "L3" });

        var violation = Assert.Single(ScheduleRules.Check([session], Policy));

        Assert.Equal(RuleCodes.TooManyAttendees, violation.Rule);
        Assert.Equal(["L1", "L2"], violation.LessonIds);
    }

    private static void AssertViolation(
        ScheduleViolation v, string rule, string date, string[] lessonIds, int sessions)
    {
        Assert.Equal(rule, v.Rule);
        Assert.Equal(DateOnly.Parse(date), v.Date);
        Assert.Equal(lessonIds, v.LessonIds);
        Assert.Equal(sessions, v.SessionIds.Count);
    }

    /// <summary>Hourly sessions for T1 in R1 on Friday 2026-03-06, from 09:00.</summary>
    private static List<RuleSession> TutorDay(int count) =>
        Enumerable.Range(0, count).Select(i => Session("T1", "R1", $"{9 + i:00}:00", 60)).ToList();

    /// <summary>A session on Friday 2026-03-06. With no attendees given, one new student.</summary>
    private static RuleSession Session(
        string tutor, string room, string start, int minutes, params RuleAttendee[] attendees)
    {
        var startsAt = DateTimeUtils.ConvertLocalToUtc(Policy.Zone, new DateOnly(2026, 3, 6), TimeOnly.Parse(start));
        return new RuleSession(
            Guid.NewGuid(), tutor, tutor, room, startsAt, startsAt.AddMinutes(minutes), Cancelled: false,
            attendees.Length > 0 ? attendees : [Booked(Guid.NewGuid().ToString())]);
    }

    /// <summary>Moves a Friday session to Monday 2026-03-09.</summary>
    private static RuleSession OnMonday(RuleSession session) =>
        session with { StartsAt = session.StartsAt.AddDays(3), EndsAt = session.EndsAt.AddDays(3) };

    private static RuleAttendee Booked(string name) => new(Guid.NewGuid(), name, AttendeeStatus.Booked, null);

    private static RuleAttendee Cancelled(string name) => new(Guid.NewGuid(), name, AttendeeStatus.Cancelled, null);
}
