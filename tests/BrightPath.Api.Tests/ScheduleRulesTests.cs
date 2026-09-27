using BrightPath.Api.Domain;
using BrightPath.Api.Seed;

namespace BrightPath.Api.Tests;

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
    }

    [Fact]
    public void Touching_sessions_do_not_overlap()
    {
        Assert.Empty(ScheduleRules.Check(
            [Session("T1", "R1", "09:00", 60), Session("T1", "R1", "10:00", 60)], Policy));
    }

    [Theory]
    [InlineData("08:30", 60, true)] // starts before opening
    [InlineData("20:00", 90, false)] // ends exactly at 21:30
    [InlineData("20:30", 90, true)] // ends at 22:00
    public void Session_must_fit_opening_hours(string start, int minutes, bool broken)
    {
        var violations = ScheduleRules.Check([Session("T1", "R1", start, minutes)], Policy);
        Assert.Equal(broken, violations.Any(v => v.Rule == RuleCodes.OutsideHours));
    }

    [Fact]
    public void Three_attendees_are_too_many_but_a_cancelled_one_does_not_count()
    {
        var three = Session("T1", "R1", "11:00", 90, Booked("A"), Booked("B"), Booked("C"));
        Assert.Equal(RuleCodes.TooManyAttendees, Assert.Single(ScheduleRules.Check([three], Policy)).Rule);

        var twoAndCancelled = Session("T1", "R1", "11:00", 90, Booked("A"), Booked("B"), Cancelled("C"));
        Assert.Empty(ScheduleRules.Check([twoAndCancelled], Policy));
    }

    [Fact]
    public void Seven_sessions_in_a_day_break_tutor_load()
    {
        var violation = Assert.Single(ScheduleRules.Check(TutorDay(7), Policy));
        Assert.Equal(RuleCodes.TutorLoad, violation.Rule);
        Assert.Equal(7, violation.SessionIds.Count);
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
        var startsAt = Policy.ToInstant(new DateOnly(2026, 3, 6), TimeOnly.Parse(start));
        return new RuleSession(
            Guid.NewGuid(), tutor, tutor, room, startsAt, startsAt.AddMinutes(minutes), Cancelled: false,
            attendees.Length > 0 ? attendees : [Booked(Guid.NewGuid().ToString())]);
    }

    /// <summary>Moves a Friday session to Monday 2026-03-09.</summary>
    private static RuleSession OnMonday(RuleSession s) =>
        s with { StartsAt = s.StartsAt.AddDays(3), EndsAt = s.EndsAt.AddDays(3) };

    private static RuleAttendee Booked(string name) => new(Guid.NewGuid(), name, AttendeeStatus.Booked, null);

    private static RuleAttendee Cancelled(string name) => new(Guid.NewGuid(), name, AttendeeStatus.Cancelled, null);
}
