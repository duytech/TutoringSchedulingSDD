using BrightPath.Api.Domain;
using BrightPath.Api.Seed;

namespace BrightPath.Api.Tests;

public sealed class ScheduleDayTests
{
    private static readonly BookingPolicy Policy = TestPolicy.Create();
    private static readonly DateTimeOffset PinnedNow = DateTimeOffset.Parse("2026-03-06T10:00:00+07:00");
    private static readonly DateOnly Friday = new(2026, 3, 6);
    private static readonly string[] Rooms = ["R1", "R2", "R3", "R4", "R5", "R6"];

    [Fact]
    public void Pinned_friday_groups_the_export_by_room_and_tutor()
    {
        var day = Export(Friday);

        Assert.Equal(Friday, day.Date);
        Assert.Equal(TimeSpan.FromHours(7), day.Now.Offset);
        Assert.Equal(10, day.Sessions.Count);

        Assert.Equal(["R1", "R2", "R3", "R4", "R5", "R6"], day.Rooms.Select(r => r.Id));
        Assert.Equal(["L018", "L021", "L022", "L024", "L025", "L026", "L027"], Lessons(day, day.Rooms[0].SessionIds));
        Assert.Equal(["L019", "L023"], Lessons(day, day.Rooms[1].SessionIds));
        Assert.Equal(["L020"], Lessons(day, day.Rooms[2].SessionIds));
        Assert.All(day.Rooms.Skip(3), r => Assert.Empty(r.SessionIds));

        Assert.Equal(["T1", "T2", "T3"], day.Tutors.Select(t => t.Id));
        Assert.Equal(["L018", "L021", "L022", "L024", "L025", "L026", "L027"], Lessons(day, day.Tutors[0].SessionIds));
        Assert.Equal(["L019", "L023"], Lessons(day, day.Tutors[1].SessionIds));
        Assert.Equal(["L020"], Lessons(day, day.Tutors[2].SessionIds));

        // L018 and L019 end exactly at 10:00, so they are over.
        Assert.Equal(["L018", "L019"], day.Sessions.Where(s => s.State == SessionState.Past).Select(Lesson));
        Assert.Equal(8, day.Sessions.Count(s => s.State == SessionState.Upcoming));

        Assert.DoesNotContain(day.Sessions, s => s.Cancelled || s.Changes.Count > 0 || s.LegacyViolation);
    }

    [Fact]
    public void A_family_cancellation_after_the_cutoff_is_shown_with_its_change()
    {
        var l005 = SessionOf(Export(new DateOnly(2026, 3, 3)), "L005");

        Assert.True(l005.Cancelled);
        var attendee = Assert.Single(l005.Attendees);
        Assert.Equal(AttendeeStatus.Cancelled, attendee.Status);
        Assert.Equal(CancelledBy.Family, attendee.CancelledBy);
        Assert.False(attendee.Chargeable);

        var change = Assert.Single(l005.Changes);
        Assert.Equal(ChangeKind.Cancelled, change.Kind);
        Assert.Equal(attendee.Id, change.AttendeeId);
        Assert.True(change.AfterCutoff);
        Assert.True(l005.ChangedAfterCutoff);
        Assert.Equal(SessionState.Past, l005.State);
    }

    [Fact]
    public void The_exam_pair_is_one_session_and_the_later_overlap_is_flagged()
    {
        var day = Export(new DateOnly(2026, 3, 4));

        var pair = SessionOf(day, "L009");
        Assert.Equal(["L009", "L010"], pair.Attendees.Select(a => a.LessonId));

        // L008 clashes only on the student (another room, another tutor), so only the attendee is flagged.
        var l008 = SessionOf(day, "L008");
        Assert.False(l008.LegacyViolation);
        Assert.True(Assert.Single(l008.Attendees).LegacyViolation);
        Assert.False(Assert.Single(SessionOf(day, "L007").Attendees).LegacyViolation);
    }

    [Fact]
    public void A_tutor_overlap_flags_the_later_session()
    {
        var day = Export(new DateOnly(2026, 3, 10));

        // L034 clashes on the tutor, so the session is flagged. Its student is free, so the attendee is not.
        var l034 = SessionOf(day, "L034");
        Assert.True(l034.LegacyViolation);
        Assert.False(Assert.Single(l034.Attendees).LegacyViolation);
        Assert.False(SessionOf(day, "L033").LegacyViolation);
        Assert.All(day.Sessions, s => Assert.Equal(SessionState.Upcoming, s.State));
    }

    [Fact]
    public void A_no_show_keeps_its_session_active()
    {
        var day = Export(new DateOnly(2026, 3, 5));

        var l015 = SessionOf(day, "L015");
        Assert.False(l015.Cancelled);
        Assert.Equal(AttendeeStatus.NoShow, Assert.Single(l015.Attendees).Status);

        var l017 = Assert.Single(SessionOf(day, "L017").Attendees);
        Assert.Equal(CancelledBy.Tutor, l017.CancelledBy);
        Assert.False(l017.Chargeable);
    }

    [Theory]
    [InlineData("09:00:00", SessionState.InProgress)] // now == start
    [InlineData("08:59:59.9999999", SessionState.Upcoming)] // one tick before start
    [InlineData("09:59:59", SessionState.InProgress)]
    [InlineData("10:00:00", SessionState.Past)] // now == end
    public void State_is_half_open(string nowLocal, string expected)
    {
        var now = Policy.ToInstant(Friday, TimeOnly.Parse(nowLocal));
        var day = Build(now, Session("R1", "09:00"));

        Assert.Equal(expected, Assert.Single(day.Sessions).State);
    }

    [Fact]
    public void Times_are_local_not_utc()
    {
        var view = Assert.Single(Build(PinnedNow, Session("R1", "09:00")).Sessions);

        Assert.Equal(DateTimeOffset.Parse("2026-03-06T09:00:00+07:00"), view.StartsAt);
        Assert.Equal(TimeSpan.FromHours(7), view.StartsAt.Offset);
        Assert.Equal(TimeSpan.FromHours(7), view.EndsAt.Offset);
        Assert.Equal(60, view.DurationMin);
    }

    [Fact]
    public void Sessions_at_the_same_time_sort_by_room()
    {
        var day = Build(PinnedNow, Session("R3", "09:00"), Session("R1", "09:00"), Session("R2", "08:00"));

        Assert.Equal(["R2", "R1", "R3"], day.Sessions.Select(s => s.RoomId));
    }

    [Fact]
    public void An_empty_day_still_lists_every_room_and_tutor()
    {
        var day = Export(new DateOnly(2026, 3, 12));

        Assert.Empty(day.Sessions);
        Assert.Equal(6, day.Rooms.Count);
        Assert.Equal(3, day.Tutors.Count);
        Assert.All(day.Rooms, r => Assert.Empty(r.SessionIds));
        Assert.All(day.Tutors, t => Assert.Empty(t.SessionIds));
    }

    [Fact]
    public void Fixed_clock_returns_the_configured_instant()
    {
        var clock = new FixedTimeProvider(PinnedNow);

        Assert.Equal(PinnedNow, clock.GetUtcNow());
        Assert.Equal(TimeSpan.Zero, clock.GetUtcNow().Offset);
    }

    /// <summary>The real export through <see cref="SeedPlanner"/>, viewed on one date at the pinned now.</summary>
    private static ScheduleDayView Export(DateOnly date)
    {
        var plan = SeedPlanner.Plan(
            SeedCsv.ReadTutors(Path.Combine(SeedCsv.Directory, "tutors.csv")),
            SeedCsv.ReadLessons(Path.Combine(SeedCsv.Directory, "lessons_export.csv")),
            Policy);
        var tutors = plan.Tutors.ToDictionary(t => t.Id, t => t.Name);
        var students = plan.Students.ToDictionary(s => s.Id, s => s.Name);
        var sessions = plan.Sessions.Select(s => new DaySession(
            s.Id, s.TutorId, tutors[s.TutorId], s.RoomId, s.StartsAt, s.EndsAt, s.CancelledAt, s.MovedToSessionId,
            s.LegacyViolation,
            s.Attendees.Select(a => new DayAttendee(
                a.Id, a.StudentId, students[a.StudentId], a.SourceLessonId, a.Status, a.CancelledAt, a.CancelledBy,
                a.Chargeable, a.LegacyViolation, a.Note)).ToList()));

        return ScheduleDay.Build(date, PinnedNow, sessions, plan.Changes, Rooms, plan.Tutors, Policy);
    }

    private static ScheduleDayView Build(DateTimeOffset now, params DaySession[] sessions) =>
        ScheduleDay.Build(Friday, now, sessions, [], Rooms, [], Policy);

    /// <summary>A one-hour session for T1 on Friday 2026-03-06.</summary>
    private static DaySession Session(string room, string start)
    {
        var startsAt = Policy.ToInstant(Friday, TimeOnly.Parse(start));
        return new DaySession(
            Guid.NewGuid(), "T1", "T1", room, startsAt, startsAt.AddMinutes(60), CancelledAt: null,
            MovedToSessionId: null, LegacyViolation: false, Attendees: []);
    }

    private static ScheduleSessionView SessionOf(ScheduleDayView day, string lessonId) =>
        day.Sessions.Single(s => s.Attendees.Any(a => a.LessonId == lessonId));

    private static string Lesson(ScheduleSessionView s) => s.Attendees[0].LessonId!;

    private static IEnumerable<string> Lessons(ScheduleDayView day, IEnumerable<Guid> sessionIds) =>
        sessionIds.Select(id => Lesson(day.Sessions.Single(s => s.Id == id)));
}
