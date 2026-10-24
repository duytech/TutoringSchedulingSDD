using System.Globalization;
using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Api.UnitTests;

/// <summary>
/// Black-box tests written from the brief ("How the centre actually operates" and the interviews), not from the code.
/// They check only which rules are broken, never the message text or the report order.
/// </summary>
public sealed class CentreRulesTests
{
    private static readonly BookingPolicy Policy = TestPolicy.Create();

    private const string Friday = "2026-03-06";
    private const string Saturday = "2026-03-07";
    private const string Monday = "2026-03-09";

    // Rooms: "A room holds one lesson at a time, whoever is teaching in it."
    [Fact]
    public void A_room_holds_one_lesson_at_a_time_whoever_is_teaching()
    {
        var rules = FindBrokenRules(Lesson("T1", "R1", Friday, "09:00", 60), Lesson("T2", "R1", Friday, "09:30", 60));
        Assert.Contains(RuleCodes.RoomOverlap, rules);
    }

    // Rooms: one lesson at a time, so the next one can start when the last one ends.
    [Fact]
    public void Back_to_back_lessons_can_share_a_room()
    {
        var rules = FindBrokenRules(Lesson("T1", "R1", Friday, "09:00", 90), Lesson("T2", "R1", Friday, "10:30", 60));
        Assert.Empty(rules);
    }

    // Rooms: "a tutor can only be in one room at a time."
    [Fact]
    public void A_tutor_can_only_be_in_one_room_at_a_time()
    {
        var rules = FindBrokenRules(Lesson("T1", "R1", Friday, "09:00", 60), Lesson("T1", "R2", Friday, "09:00", 60));
        Assert.Contains(RuleCodes.TutorOverlap, rules);
    }

    [Fact]
    public void Different_tutors_can_teach_in_different_rooms_at_the_same_time()
    {
        var rules = FindBrokenRules(Lesson("T1", "R1", Friday, "09:00", 60), Lesson("T2", "R2", Friday, "09:00", 60));
        Assert.Empty(rules);
    }

    // Owner: "a student booked into two places at once ... can never happen again."
    [Fact]
    public void A_student_cannot_be_booked_into_two_places_at_once()
    {
        var student = Student("Le Minh Chau");
        var rules = FindBrokenRules(
            Lesson("T3", "R3", Friday, "09:00", 60, student), Lesson("T2", "R2", Friday, "09:30", 60, student));
        Assert.Contains(RuleCodes.StudentOverlap, rules);
    }

    [Fact]
    public void A_student_can_have_two_lessons_in_a_day_at_different_times()
    {
        var student = Student("Le Minh Chau");
        var rules = FindBrokenRules(
            Lesson("T3", "R3", Friday, "09:00", 60, student), Lesson("T2", "R2", Friday, "14:00", 60, student));
        Assert.Empty(rules);
    }

    // Receptionist: "two students with one tutor in the same room and the same slot, on purpose."
    [Fact]
    public void An_exam_pair_shares_one_tutor_room_and_slot()
    {
        var rules = FindBrokenRules(Lesson("T1", "R1", Friday, "11:00", 90, Student("A"), Student("B")));
        Assert.Empty(rules);
    }

    // Tutor load: "No tutor may be given more than 6 bookings in a single day."
    [Theory]
    [InlineData(6, false)]
    [InlineData(7, true)]
    public void A_tutor_may_have_at_most_six_bookings_a_day(int bookings, bool broken)
    {
        var rules = FindBrokenRules([.. TutorDay("T1", "R1", Friday, bookings)]);
        Assert.Equal(broken, rules.Contains(RuleCodes.TutorLoad));
    }

    [Fact]
    public void The_tutor_limit_is_per_day()
    {
        var rules = FindBrokenRules([.. TutorDay("T1", "R1", Friday, 6), .. TutorDay("T1", "R1", Saturday, 6)]);
        Assert.Empty(rules);
    }

    [Fact]
    public void The_tutor_limit_is_per_tutor()
    {
        var rules = FindBrokenRules([.. TutorDay("T1", "R1", Friday, 6), .. TutorDay("T2", "R2", Friday, 6)]);
        Assert.Empty(rules);
    }

    // Opening days: "The centre teaches Tuesday to Sunday. It is closed on Monday."
    [Theory]
    [InlineData("2026-03-03", false)] // Tuesday
    [InlineData("2026-03-04", false)] // Wednesday
    [InlineData("2026-03-05", false)] // Thursday
    [InlineData("2026-03-06", false)] // Friday
    [InlineData("2026-03-07", false)] // Saturday
    [InlineData("2026-03-08", false)] // Sunday
    [InlineData("2026-03-09", true)] // Monday
    public void The_centre_teaches_tuesday_to_sunday(string date, bool broken)
    {
        var rules = FindBrokenRules(Lesson("T1", "R1", date, "10:00", 60));
        Assert.Equal(broken, rules.Contains(RuleCodes.ClosedDay));
    }

    // Opening days: "Lessons run from mid-morning to mid-evening." Times clearly inside or outside, not the exact edge.
    [Theory]
    [InlineData("07:00", true)] // early morning
    [InlineData("14:00", false)] // afternoon
    [InlineData("22:00", true)] // night
    public void Lessons_run_from_mid_morning_to_mid_evening(string start, bool broken)
    {
        var rules = FindBrokenRules(Lesson("T1", "R1", Friday, start, 60));
        Assert.Equal(broken, rules.Contains(RuleCodes.OutsideHours));
    }

    // Late cancellation: "Cancelling frees the room and the slot."
    [Fact]
    public void Cancelling_frees_the_room_and_the_slot()
    {
        var cancelled = Lesson("T1", "R1", Friday, "09:00", 60) with { Cancelled = true };
        var rules = FindBrokenRules(cancelled, Lesson("T2", "R1", Friday, "09:00", 60));
        Assert.Empty(rules);
    }

    [Fact]
    public void A_cancelled_student_frees_their_slot()
    {
        var student = Student("Le Minh Chau");
        var cancelledStudent = student with { Status = AttendeeStatus.Cancelled };
        var rules = FindBrokenRules(
            Lesson("T1", "R1", Friday, "09:00", 60, cancelledStudent, Student("B")),
            Lesson("T2", "R2", Friday, "09:00", 60, student));
        Assert.Empty(rules);
    }

    // Late cancellation: "a student who simply does not arrive is a no show, which frees neither."
    [Fact]
    public void A_no_show_keeps_the_students_slot()
    {
        var student = Student("Le Minh Chau");
        var noShow = student with { Status = AttendeeStatus.NoShow };
        var rules = FindBrokenRules(
            Lesson("T1", "R1", Friday, "09:00", 60, noShow), Lesson("T2", "R2", Friday, "09:00", 60, student));
        Assert.Contains(RuleCodes.StudentOverlap, rules);
    }

    [Fact]
    public void A_no_show_keeps_the_room()
    {
        var noShow = Student("A") with { Status = AttendeeStatus.NoShow };
        var rules = FindBrokenRules(
            Lesson("T1", "R1", Friday, "09:00", 60, noShow), Lesson("T2", "R1", Friday, "09:00", 60));
        Assert.Contains(RuleCodes.RoomOverlap, rules);
    }

    // Late cancellation: "A family may cancel free of charge up to 4 hours before the lesson starts."
    [Theory]
    [InlineData(300, false)]
    [InlineData(240, false)]
    [InlineData(239, true)]
    [InlineData(60, true)]
    public void A_family_cancels_free_up_to_four_hours_before(int minutesBefore, bool charged)
    {
        var startsAt = LocalInstant(Friday, "14:00");
        var cancelledAt = startsAt.AddMinutes(-minutesBefore);
        Assert.Equal(charged, Policy.IsChargeable(CancelledBy.Family, cancelledAt, startsAt));
    }

    // Cut-off: "Tomorrow's schedule is final at 16:00 today."
    [Theory]
    [InlineData("2026-03-05", "10:00", false)] // two days before
    [InlineData("2026-03-06", "15:59", false)] // the day before, just before the cut-off
    [InlineData("2026-03-06", "16:00", true)] // the day before, at the cut-off
    [InlineData("2026-03-07", "08:00", true)] // the same morning
    public void Tomorrows_schedule_is_final_at_16_00_today(string changeDate, string changeTime, bool afterCutoff)
    {
        var startsAt = LocalInstant(Saturday, "14:00");
        var changedAt = LocalInstant(changeDate, changeTime);
        Assert.Equal(afterCutoff, Policy.IsAfterCutoff(changedAt, startsAt));
    }

    // Readings we chose: these depend on an answer in DECISIONS.md, not on the brief alone.

    // Q1: an exam pair counts as 1 booking toward the 6-per-day limit.
    [Fact]
    public void An_exam_pair_counts_as_one_booking_toward_the_limit()
    {
        var sessions = TutorDay("T1", "R1", Friday, 6);
        sessions[0] = sessions[0] with { Attendees = [Student("A"), Student("B")] };
        Assert.Empty(FindBrokenRules([.. sessions]));
    }

    // Q7: an exam pair is at most two students.
    [Fact]
    public void An_exam_pair_is_at_most_two_students()
    {
        var rules = FindBrokenRules(Lesson("T1", "R1", Friday, "11:00", 90, Student("A"), Student("B"), Student("C")));
        Assert.Contains(RuleCodes.TooManyAttendees, rules);
    }

    // Q2: only a family cancellation is charged; the brief only says what a family pays.
    [Theory]
    [InlineData(CancelledBy.Tutor)]
    [InlineData(CancelledBy.Centre)]
    public void Only_a_family_cancellation_is_charged(string cancelledBy)
    {
        var startsAt = LocalInstant(Friday, "14:00");
        var cancelledAt = startsAt.AddHours(-1);
        Assert.False(Policy.IsChargeable(cancelledBy, cancelledAt, startsAt));
    }

    // DECISIONS §1: the cut-off is the calendar day before, even when that day is the closed Monday.
    [Fact]
    public void The_cut_off_before_a_tuesday_is_monday_16_00()
    {
        var startsAt = LocalInstant("2026-03-10", "09:00");
        var changedAt = LocalInstant(Monday, "16:00");
        Assert.True(Policy.IsAfterCutoff(changedAt, startsAt));
    }

    private static List<string> FindBrokenRules(params RuleSession[] sessions)
    {
        var violations = ScheduleRules.Check(sessions, Policy);
        return violations.Select(violation => violation.Rule).ToList();
    }

    /// <summary>A lesson at a local date and time. With no students given, one new student.</summary>
    private static RuleSession Lesson(
        string tutor, string room, string date, string start, int minutes, params RuleAttendee[] students)
    {
        var startsAt = LocalInstant(date, start);
        var attendees = students.Length > 0 ? students : [Student(Guid.NewGuid().ToString())];
        return new RuleSession(
            Guid.NewGuid(), tutor, tutor, room, startsAt, startsAt.AddMinutes(minutes), Cancelled: false, attendees);
    }

    /// <summary>Hourly lessons for one tutor in one room, from 09:00.</summary>
    private static List<RuleSession> TutorDay(string tutor, string room, string date, int count) =>
        Enumerable.Range(0, count).Select(hour => Lesson(tutor, room, date, $"{9 + hour:00}:00", 60)).ToList();

    private static RuleAttendee Student(string name) => new(Guid.NewGuid(), name, AttendeeStatus.Booked, null);

    private static DateTimeOffset LocalInstant(string date, string time)
    {
        var localDate = DateOnly.Parse(date, CultureInfo.InvariantCulture);
        var localTime = TimeOnly.Parse(time, CultureInfo.InvariantCulture);
        return DateTimeUtils.ConvertLocalToUtc(Policy.Zone, localDate, localTime);
    }
}
