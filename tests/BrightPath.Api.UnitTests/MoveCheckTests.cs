using BrightPath.Domain;
using BrightPath.Infrastructure.Seed;

namespace BrightPath.Api.UnitTests;

public sealed class MoveCheckTests
{
    private static readonly BookingPolicy Policy = TestPolicy.Create();
    private static readonly DateTimeOffset PinnedNow = DateTimeOffset.Parse("2026-03-06T10:00:00+07:00");
    private static readonly SeedPlan Plan = SeedPlanner.Plan(
        SeedCsv.ReadTutors(Path.Combine(SeedCsv.Directory, "tutors.csv")),
        SeedCsv.ReadLessons(Path.Combine(SeedCsv.Directory, "lessons_export.csv")),
        Policy);

    // Rows of the expected-results table in the phase 18 spec, each on the untouched export.
    [Theory]
    [InlineData("L028", "2026-03-07 14:00", null, "")] // row 1
    [InlineData("L020", "2026-03-06 16:00", null, "")] // row 2
    [InlineData("L029", "2026-03-07 11:30", null, "")] // row 3: overlaps its own old slot
    [InlineData("L030", "2026-03-07 09:30", "R3", "room-overlap")] // row 4, against L028 as exported
    [InlineData("L034", "2026-03-10 10:00", null, "")] // row 5: the flagged session
    [InlineData("L021", "2026-03-06 14:30", "R4", "tutor-load")] // row 6: T1 still has 7
    [InlineData("L030", "2026-03-09 10:00", null, "closed-day")] // row 7
    [InlineData("L030", "2026-03-05 10:00", null, "in-the-past")] // row 8
    [InlineData("L018", "2026-03-07 10:00", "R4", "already-started")] // row 9
    [InlineData("L005", "2026-03-07 16:00", null, "already-started,already-cancelled")] // row 11
    public void Move_on_the_export_gets_exactly_its_conflicts(string lessonId, string start, string? room, string expected)
    {
        var (source, candidate) = Move(lessonId, start, room);

        var conflicts = MoveCheck.Conflicts(source, candidate, SameDay(candidate), PinnedNow, Policy);

        Assert.Equal(expected.Split(',', StringSplitOptions.RemoveEmptyEntries), conflicts.Select(c => c.Rule));
    }

    [Fact]
    public void A_session_already_moved_cannot_be_moved_again()
    {
        var (source, candidate) = Move("L028", "2026-03-07 17:00", null);
        var moved = source with { CancelledAt = PinnedNow };

        var conflict = Assert.Single(MoveCheck.Conflicts(moved, candidate, SameDay(candidate), PinnedNow, Policy));

        Assert.Equal(RuleCodes.AlreadyCancelled, conflict.Rule);
        Assert.Equal("The session was already cancelled at 2026-03-06 10:00.", conflict.Message);
        Assert.Equal(["L028"], conflict.LessonIds);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void A_session_counts_as_started_from_its_first_minute(int minutesBefore, bool refused)
    {
        var (source, candidate) = Move("L028", "2026-03-07 14:00", null);

        var conflicts = MoveCheck.Conflicts(
            source, candidate, SameDay(candidate), source.StartsAt.AddMinutes(-minutesBefore), Policy);

        Assert.Equal(refused, conflicts.Any(c => c.Rule == RuleCodes.AlreadyStarted));
    }

    [Fact]
    public void Nothing_to_move_when_time_room_and_length_are_the_same()
    {
        var (source, _) = Move("L028", "2026-03-07 09:00", null);

        Assert.True(MoveCheck.IsNoop(source, source.StartsAt, "R3", 90));
        Assert.False(MoveCheck.IsNoop(source, source.StartsAt, "R4", 90));
        Assert.False(MoveCheck.IsNoop(source, source.StartsAt, "R3", 60));
    }

    [Fact]
    public void The_notes_say_where_it_went_and_where_it_came_from()
    {
        var (source, candidate) = Move("L028", "2026-03-07 16:00", "R4");

        var (onOld, onNew) = MoveCheck.Notes(source, candidate.StartsAt, "R4", "exam moved", Policy);

        Assert.Equal("to 2026-03-07 16:00 in R4; exam moved", onOld);
        Assert.Equal("from 2026-03-07 09:00 in R3; exam moved", onNew);
    }

    /// <summary>The exported session holding <paramref name="lessonId"/>, and where it would go. Length and tutor stay.</summary>
    private static (MoveSource Source, RuleSession Candidate) Move(string lessonId, string start, string? room)
    {
        var session = Plan.Sessions.Single(s => s.Attendees.Any(a => a.SourceLessonId == lessonId));
        var names = Plan.Students.ToDictionary(s => s.Id, s => s.Name);
        var source = new MoveSource(
            session.Id, session.RoomId, session.StartsAt, session.EndsAt, session.CancelledAt,
            session.Attendees.Select(a => a.SourceLessonId).OfType<string>().ToList());

        var local = DateTime.Parse(start, System.Globalization.CultureInfo.InvariantCulture);
        var startsAt = DateTimeUtils.LocalToUtc(
            Policy.Zone, DateOnly.FromDateTime(local), TimeOnly.FromDateTime(local));
        var candidate = new RuleSession(
            Guid.NewGuid(), session.TutorId, Plan.Tutors.Single(t => t.Id == session.TutorId).Name,
            room ?? session.RoomId, startsAt, startsAt + (session.EndsAt - session.StartsAt), Cancelled: false,
            session.Attendees
                .Where(a => a.Status == AttendeeStatus.Booked)
                .Select(a => new RuleAttendee(a.StudentId, names[a.StudentId], AttendeeStatus.Booked, null))
                .ToList());
        return (source, candidate);
    }

    /// <summary>The export's active sessions on the candidate's local date, as the endpoint loads them.</summary>
    private static List<RuleSession> SameDay(RuleSession candidate)
    {
        var date = DateTimeUtils.LocalDate(Policy.Zone, candidate.StartsAt);
        var tutors = Plan.Tutors.ToDictionary(t => t.Id, t => t.Name);
        var students = Plan.Students.ToDictionary(s => s.Id, s => s.Name);
        return Plan.Sessions
            .Where(s => s.CancelledAt is null && DateTimeUtils.LocalDate(Policy.Zone, s.StartsAt) == date)
            .Select(s => new RuleSession(
                s.Id, s.TutorId, tutors[s.TutorId], s.RoomId, s.StartsAt, s.EndsAt, Cancelled: false,
                s.Attendees
                    .Select(a => new RuleAttendee(a.StudentId, students[a.StudentId], a.Status, a.SourceLessonId))
                    .ToList()))
            .ToList();
    }
}
