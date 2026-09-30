using BrightPath.Domain;
using BrightPath.Infrastructure.Seed;

namespace BrightPath.Api.UnitTests;

public sealed class BookingCheckTests
{
    private static readonly BookingPolicy Policy = TestPolicy.Create();
    private static readonly DateTimeOffset PinnedNow = DateTimeOffset.Parse("2026-03-06T10:00:00+07:00");
    private static readonly SeedPlan Plan = SeedPlanner.Plan(
        SeedCsv.ReadTutors(Path.Combine(SeedCsv.Directory, "tutors.csv")),
        SeedCsv.ReadLessons(Path.Combine(SeedCsv.Directory, "lessons_export.csv")),
        Policy);

    // The rows of the expected-results table in the phase 11 spec, checked on the real export.
    [Theory]
    [InlineData("T2", "R4", "2026-03-07 13:00", 60, "Vu Ha My", "")] // row 1
    [InlineData("T2", "R4", "2026-03-06 16:00", 60, "Vu Ha My", "")] // row 2: T1's load break that day is not ours
    [InlineData("T3", "R5", "2026-03-07 14:00", 90, "Nguyen Thi Ha|Do Van Kien", "")] // row 3: exam pair
    [InlineData("T3", "R3", "2026-03-07 10:00", 60, "Tran Bao Long", "room-overlap,tutor-overlap,student-overlap")] // row 5
    [InlineData("T1", "R4", "2026-03-06 14:30", 60, "Do Van Kien", "tutor-load")] // row 6
    [InlineData("T2", "R2", "2026-03-09 10:00", 60, "Le Minh Chau", "closed-day")] // row 7
    [InlineData("T2", "R4", "2026-03-07 20:30", 90, "Vu Ha My", "outside-hours")] // row 8
    [InlineData("T2", "R6", "2026-03-08 14:00", 60, "Vu Ha My|Le Minh Chau|Bui An Nhien", "too-many-attendees")] // row 9
    [InlineData("T2", "R4", "2026-03-05 11:00", 60, "Vu Ha My", "in-the-past")] // row 10
    [InlineData("T2", "R4", "2026-03-10 09:00", 60, "Le Minh Chau", "student-overlap")] // row 11
    [InlineData("T3", "R4", "2026-03-10 09:00", 60, "Vu Ha My", "")] // row 12: L034 is T1 in R2
    public void Booking_on_the_export_gets_exactly_its_conflicts(
        string tutor, string room, string start, int minutes, string students, string expected)
    {
        var candidate = Candidate(tutor, room, start, minutes, students.Split('|'));

        var conflicts = BookingCheck.Conflicts(candidate, SameDay(candidate), PinnedNow, Policy);

        Assert.Equal(expected.Split(',', StringSplitOptions.RemoveEmptyEntries), conflicts.Select(c => c.Rule));
        Assert.All(conflicts, c => Assert.Contains(candidate.Id, c.SessionIds));
    }

    [Fact]
    public void Three_overlaps_all_name_the_lesson_in_the_way()
    {
        var candidate = Candidate("T3", "R3", "2026-03-07 10:00", 60, "Tran Bao Long");

        var conflicts = BookingCheck.Conflicts(candidate, SameDay(candidate), PinnedNow, Policy);

        Assert.All(conflicts, c => Assert.Equal(["L028"], c.LessonIds));
        Assert.Equal("R3 holds two sessions at once: T3 at 09:00 and T3 at 10:00.", conflicts[0].Message);
    }

    [Fact]
    public void An_eighth_session_lists_the_whole_day_of_the_tutor()
    {
        var candidate = Candidate("T1", "R4", "2026-03-06 14:30", 60, "Do Van Kien");

        var load = Assert.Single(BookingCheck.Conflicts(candidate, SameDay(candidate), PinnedNow, Policy));

        Assert.Equal(8, load.SessionIds.Count);
        Assert.Equal("T1 Ngoc Anh has 8 sessions on 2026-03-06; the limit is 6.", load.Message);
    }

    [Fact]
    public void The_same_booking_twice_overlaps_itself()
    {
        var first = Candidate("T2", "R4", "2026-03-07 13:00", 60, "Vu Ha My");
        var again = first with { Id = Guid.NewGuid() };

        var conflicts = BookingCheck.Conflicts(again, [.. SameDay(first), first], PinnedNow, Policy);

        Assert.Equal(
            [RuleCodes.RoomOverlap, RuleCodes.TutorOverlap, RuleCodes.StudentOverlap], conflicts.Select(c => c.Rule));
    }

    [Fact]
    public void A_cancelled_session_does_not_block_its_slot()
    {
        var first = Candidate("T2", "R4", "2026-03-07 13:00", 60, "Vu Ha My");
        var again = first with { Id = Guid.NewGuid() };

        Assert.Empty(BookingCheck.Conflicts(again, [.. SameDay(first), first with { Cancelled = true }], PinnedNow, Policy));
    }

    [Fact]
    public void Starting_exactly_now_is_not_in_the_past()
    {
        // L018 and L019 end at 10:00, so touching them is fine too.
        var candidate = Candidate("T2", "R4", "2026-03-06 10:00", 60, "Vu Ha My");

        Assert.Empty(BookingCheck.Conflicts(candidate, SameDay(candidate), PinnedNow, Policy));
    }

    [Fact]
    public void One_minute_before_now_is_in_the_past_and_listed_first()
    {
        var candidate = Candidate("T2", "R4", "2026-03-06 09:59", 60, "Vu Ha My");

        var conflicts = BookingCheck.Conflicts(candidate, SameDay(candidate), PinnedNow, Policy);

        Assert.Equal(RuleCodes.InThePast, conflicts[0].Rule);
        Assert.Equal("The session starts at 2026-03-06 09:59, before now (2026-03-06 10:00).", conflicts[0].Message);
        Assert.Empty(conflicts[0].LessonIds);
    }

    /// <summary>A new session at a local date and time, for students named in the export.</summary>
    private static RuleSession Candidate(string tutor, string room, string start, int minutes, params string[] students)
    {
        var local = DateTime.Parse(start, System.Globalization.CultureInfo.InvariantCulture);
        var startsAt = Policy.ToInstant(DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local));
        var names = Plan.Students.ToDictionary(s => s.Name, s => s.Id);
        return new RuleSession(
            Guid.NewGuid(), tutor, Plan.Tutors.Single(t => t.Id == tutor).Name, room, startsAt,
            startsAt.AddMinutes(minutes), Cancelled: false,
            students.Select(n => new RuleAttendee(names[n], n, AttendeeStatus.Booked, null)).ToList());
    }

    /// <summary>The export's active sessions on the candidate's local date, as the endpoint loads them.</summary>
    private static List<RuleSession> SameDay(RuleSession candidate)
    {
        var date = Policy.LocalDate(candidate.StartsAt);
        var tutors = Plan.Tutors.ToDictionary(t => t.Id, t => t.Name);
        var students = Plan.Students.ToDictionary(s => s.Id, s => s.Name);
        return Plan.Sessions
            .Where(s => s.CancelledAt is null && Policy.LocalDate(s.StartsAt) == date)
            .Select(s => new RuleSession(
                s.Id, s.TutorId, tutors[s.TutorId], s.RoomId, s.StartsAt, s.EndsAt, Cancelled: false,
                s.Attendees
                    .Select(a => new RuleAttendee(a.StudentId, students[a.StudentId], a.Status, a.SourceLessonId))
                    .ToList()))
            .ToList();
    }
}
