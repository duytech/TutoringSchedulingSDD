using BrightPath.Api.UnitTests.Infrastructure;
using BrightPath.Application.Sessions;
using BrightPath.Common;
using BrightPath.Domain;
using BrightPath.Infrastructure.Time;

namespace BrightPath.Api.UnitTests;

public sealed class DaySessionsTests
{
    private static readonly BookingPolicy Policy = TestPolicy.Create();
    private static readonly DateTimeOffset PinnedNow = DateTimeOffset.Parse("2026-03-06T10:00:00+07:00");
    private static readonly DateOnly Friday = new(2026, 3, 6);

    [Fact]
    public void Pinned_friday_shows_the_export_for_that_day()
    {
        var day = Export(Friday);

        Assert.Equal(10, day.Count);

        Assert.Equal(["L018", "L021", "L022", "L024", "L025", "L026", "L027"], Lessons(day, s => s.RoomId == "R1"));
        Assert.Equal(["L019", "L023"], Lessons(day, s => s.RoomId == "R2"));
        Assert.Equal(["L020"], Lessons(day, s => s.RoomId == "R3"));

        Assert.Equal(["L018", "L021", "L022", "L024", "L025", "L026", "L027"], Lessons(day, s => s.TutorId == "T1"));
        Assert.Equal(["L019", "L023"], Lessons(day, s => s.TutorId == "T2"));
        Assert.Equal(["L020"], Lessons(day, s => s.TutorId == "T3"));

        Assert.DoesNotContain(day, s => s.CancelledAt is not null || s.Changes.Count > 0 || s.LegacyViolation);
    }

    [Fact]
    public void A_family_cancellation_after_the_cutoff_is_shown_with_its_change()
    {
        var l005 = SessionOf(Export(new DateOnly(2026, 3, 3)), "L005");

        Assert.NotNull(l005.CancelledAt);
        var attendee = Assert.Single(l005.Attendees);
        Assert.Equal(AttendeeStatus.Cancelled, attendee.Status);
        Assert.Equal(CancelledBy.Family, attendee.CancelledBy);
        Assert.False(attendee.Chargeable);

        var change = Assert.Single(l005.Changes);
        Assert.Equal(ChangeKind.Cancelled, change.Kind);
        Assert.Equal(attendee.Id, change.AttendeeId);
        Assert.True(change.AfterCutoff);
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
    }

    [Fact]
    public void A_no_show_keeps_its_session_active()
    {
        var day = Export(new DateOnly(2026, 3, 5));

        var l015 = SessionOf(day, "L015");
        Assert.Null(l015.CancelledAt);
        Assert.Equal(AttendeeStatus.NoShow, Assert.Single(l015.Attendees).Status);

        var l017 = Assert.Single(SessionOf(day, "L017").Attendees);
        Assert.Equal(CancelledBy.Tutor, l017.CancelledBy);
        Assert.False(l017.Chargeable);
    }

    [Fact]
    public void Times_are_local_not_utc()
    {
        var view = Assert.Single(Build(Session("R1", "09:00")));

        Assert.Equal(DateTimeOffset.Parse("2026-03-06T09:00:00+07:00"), view.StartsAt);
        Assert.Equal(TimeSpan.FromHours(7), view.StartsAt.Offset);
        Assert.Equal(DateTimeOffset.Parse("2026-03-06T10:00:00+07:00"), view.EndsAt);
        Assert.Equal(TimeSpan.FromHours(7), view.EndsAt.Offset);
    }

    [Fact]
    public void Sessions_at_the_same_time_sort_by_room()
    {
        var day = Build(Session("R3", "09:00"), Session("R1", "09:00"), Session("R2", "08:00"));

        Assert.Equal(["R2", "R1", "R3"], day.Select(s => s.RoomId));
    }

    [Fact]
    public void An_empty_day_has_no_sessions()
    {
        var day = Export(new DateOnly(2026, 3, 12));

        Assert.Empty(day);
    }

    [Fact]
    public void Fixed_clock_returns_the_configured_instant()
    {
        var clock = new FixedTimeProvider(PinnedNow);

        Assert.Equal(PinnedNow, clock.GetUtcNow());
        Assert.Equal(TimeSpan.Zero, clock.GetUtcNow().Offset);
    }

    /// <summary>The real export, viewed on one date at the pinned now.</summary>
    private static List<SessionView> Export(DateOnly date)
    {
        var export = SeedExport.Load(Policy);
        return DaySessions.Build(date, export.ForDaySessions, export.Changes, Policy);
    }

    private static List<SessionView> Build(params GetDaySessionsResponse[] sessions) =>
        DaySessions.Build(Friday, sessions, [], Policy);

    /// <summary>A one-hour session for T1 on Friday 2026-03-06.</summary>
    private static GetDaySessionsResponse Session(string room, string start)
    {
        var startsAt = DateTimeUtils.LocalToUtc(Policy.Zone, Friday, TimeOnly.Parse(start));
        return new GetDaySessionsResponse(
            Guid.NewGuid(), "T1", "T1", room, startsAt, startsAt.AddMinutes(60), CancelledAt: null,
            MovedToSessionId: null, LegacyViolation: false, Attendees: []);
    }

    private static SessionView SessionOf(List<SessionView> day, string lessonId) =>
        day.Single(s => s.Attendees.Any(a => a.LessonId == lessonId));

    private static string Lesson(SessionView s) => s.Attendees[0].LessonId!;

    private static IEnumerable<string> Lessons(List<SessionView> day, Func<SessionView, bool> which) =>
        day.Where(which).Select(Lesson);
}
