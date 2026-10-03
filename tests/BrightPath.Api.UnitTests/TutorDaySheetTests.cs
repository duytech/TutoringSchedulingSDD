using System.Text.Json;
using BrightPath.Api.UnitTests.Infrastructure;
using BrightPath.Application.Sessions;
using BrightPath.Application.Tutors;
using BrightPath.Domain;

namespace BrightPath.Api.UnitTests;

public sealed class TutorDaySheetTests
{
    private static readonly BookingPolicy Policy = TestPolicy.Create();
    private static readonly DateTimeOffset PinnedNow = DateTimeOffset.Parse("2026-03-06T10:00:00+07:00");
    private static readonly DateOnly Friday = new(2026, 3, 6);
    private static readonly SeedExport Seed = SeedExport.Load(Policy);
    private static readonly Tutor T1 = new() { Id = "T1", Name = "Ngoc Anh", Subject = "Maths" };

    [Fact]
    public void T1_on_the_pinned_friday_has_all_seven_and_nothing_changed_late()
    {
        var sheet = Export("T1", Friday);

        Assert.Equal("Ngoc Anh", sheet.TutorName);
        Assert.Equal(DateTimeOffset.Parse("2026-03-05T16:00:00+07:00"), sheet.Cutoff);
        Assert.Equal(TimeSpan.FromHours(7), sheet.Cutoff.Offset);
        Assert.True(sheet.Final);
        Assert.Equal(["L018", "L021", "L022", "L024", "L025", "L026", "L027"], sheet.Sessions.Select(Lesson));
        Assert.Equal(["L018"], sheet.Sessions.Where(s => s.State == SessionState.Past).Select(Lesson));
        Assert.Empty(sheet.ChangesAfterCutoff);
    }

    [Fact]
    public void A_tutor_cancellation_on_the_day_is_listed_with_the_student()
    {
        var sheet = Export("T3", new DateOnly(2026, 3, 5));

        Assert.Equal(["L014", "L017"], sheet.Sessions.Select(Lesson));
        Assert.True(sheet.Sessions[1].Cancelled);
        var change = Assert.Single(sheet.ChangesAfterCutoff);
        Assert.Equal(sheet.Sessions[1].Id, change.SessionId);
        Assert.Equal(DateTimeOffset.Parse("2026-03-05T16:00:00+07:00"), change.SessionStartsAt);
        Assert.Equal("R3", change.RoomId);
        Assert.Equal(ChangeKind.Cancelled, change.Kind);
        Assert.Equal("Do Van Kien", change.StudentName);
        Assert.Equal(CancelledBy.Tutor, change.ChangedBy);
        Assert.Equal(DateTimeOffset.Parse("2026-03-05T14:40:00+07:00"), change.ChangedAt);
        Assert.Equal("tutor sick", change.Note);
    }

    [Fact]
    public void A_family_cancellation_is_listed_too()
    {
        var sheet = Export("T2", new DateOnly(2026, 3, 3));

        Assert.Equal(["L002", "L005"], sheet.Sessions.Select(Lesson));
        var change = Assert.Single(sheet.ChangesAfterCutoff);
        Assert.Equal(CancelledBy.Family, change.ChangedBy);
        Assert.Equal(DateTimeOffset.Parse("2026-03-03T08:15:00+07:00"), change.ChangedAt);
    }

    [Fact]
    public void The_exam_pair_is_one_session_and_flagged_rows_stay_in()
    {
        var pair = Assert.Single(Export("T1", new DateOnly(2026, 3, 4)).Sessions);
        Assert.Equal(["L009", "L010"], pair.Attendees.Select(a => a.LessonId));

        Assert.Equal(["L008", "L012"], Export("T2", new DateOnly(2026, 3, 4)).Sessions.Select(Lesson));

        var tenth = Export("T1", new DateOnly(2026, 3, 10)).Sessions;
        Assert.Equal(["L033", "L034"], tenth.Select(Lesson));
        Assert.Equal(["R1", "R2"], tenth.Select(s => s.RoomId));
        Assert.Equal([false, true], tenth.Select(s => s.LegacyViolation));
    }

    [Fact]
    public void Tomorrow_is_not_final_before_the_cutoff()
    {
        var sheet = Export("T3", new DateOnly(2026, 3, 7));

        Assert.Equal(["L028"], sheet.Sessions.Select(Lesson));
        Assert.Equal(DateTimeOffset.Parse("2026-03-06T16:00:00+07:00"), sheet.Cutoff);
        Assert.False(sheet.Final);
    }

    [Fact]
    public void A_day_off_is_empty()
    {
        var sheet = Export("T2", new DateOnly(2026, 3, 9));

        Assert.Empty(sheet.Sessions);
        Assert.Empty(sheet.ChangesAfterCutoff);
    }

    [Theory]
    [InlineData("2026-03-05T16:00:00+07:00", true)] // exactly at the cut-off
    [InlineData("2026-03-05T15:59:00+07:00", false)]
    public void A_change_counts_from_the_cutoff_on(string changedAt, bool listed)
    {
        var session = Session("T1", "R1", Friday, "14:00");
        var change = Change(session, DateTimeOffset.Parse(changedAt));

        var sheet = Build(PinnedNow, [session], [change]);

        Assert.Equal(listed, sheet.ChangesAfterCutoff.Count == 1);
    }

    [Theory]
    [InlineData("2026-03-05T16:00:00+07:00", true)]
    [InlineData("2026-03-05T15:59:59+07:00", false)]
    public void Final_flips_at_the_cutoff(string now, bool final) =>
        Assert.Equal(final, Build(DateTimeOffset.Parse(now), [], []).Final);

    [Fact]
    public void Other_tutors_and_the_next_midnight_are_left_out()
    {
        var mine = Session("T1", "R1", Friday, "14:00");
        var theirs = Session("T2", "R2", Friday, "14:00");
        var nextMidnight = Session("T1", "R1", Friday.AddDays(1), "00:00");

        var sheet = Build(PinnedNow, [nextMidnight, theirs, mine], []);

        Assert.Equal([mine.Id], sheet.Sessions.Select(s => s.Id));
    }

    [Fact]
    public void Changes_are_oldest_first_with_the_student_before_the_session()
    {
        var early = Session("T1", "R1", Friday, "14:00");
        var late = Session("T1", "R2", Friday, "11:00");
        var at = DateTimeOffset.Parse("2026-03-06T09:00:00+07:00");
        var attendee = Guid.NewGuid();
        var changes = new[]
        {
            Change(early, at), // the session goes when its last student does
            Change(early, at, attendee),
            Change(late, at.AddMinutes(-30)),
        };

        var sheet = Build(PinnedNow, [early, late], changes);

        Assert.Equal([late.Id, early.Id, early.Id], sheet.ChangesAfterCutoff.Select(c => c.SessionId));
        Assert.Equal(attendee, sheet.ChangesAfterCutoff[1].AttendeeId);
        Assert.Null(sheet.ChangesAfterCutoff[2].AttendeeId);
        Assert.Null(sheet.ChangesAfterCutoff[2].StudentName);
    }

    [Fact]
    public void Every_seeded_session_shows_the_same_on_the_sheet_as_in_the_day_sessions()
    {
        var moveTargets = Seed.ForDaySessions.ToDictionary(s => s.Id, s => new MovedToView(s.Id, s.StartsAt, s.RoomId));
        var dates = Seed.ForDaySessions
            .Select(s => DateTimeUtils.LocalDate(Policy.Zone, s.StartsAt))
            .Distinct()
            .ToList();

        foreach (var date in dates)
        {
            var day = DaySessions.Build(date, PinnedNow, Seed.ForDaySessions, Seed.Changes, Policy, moveTargets);
            foreach (var tutor in Seed.Tutors)
            {
                var sheet = TutorDaySheet.Build(
                    tutor, date, PinnedNow, Seed.ForTutorDay, Seed.Changes, Policy, moveTargets);
                var inDay = day.Sessions.Where(s => s.TutorId == tutor.Id);

                Assert.Equal(JsonSerializer.Serialize(inDay), JsonSerializer.Serialize(sheet.Sessions));
            }
        }
    }

    private static TutorDaySheetView Export(string tutorId, DateOnly date) =>
        TutorDaySheet.Build(Seed.Tutors.Single(t => t.Id == tutorId), date, PinnedNow, Seed.ForTutorDay, Seed.Changes, Policy);

    private static TutorDaySheetView Build(DateTimeOffset now, GetTutorDayResponse[] sessions, BookingChange[] changes) =>
        TutorDaySheet.Build(T1, Friday, now, sessions, changes, Policy);

    private static GetTutorDayResponse Session(string tutorId, string room, DateOnly date, string start)
    {
        var startsAt = DateTimeUtils.LocalToUtc(Policy.Zone, date, TimeOnly.Parse(start));
        return new GetTutorDayResponse(
            Guid.NewGuid(), tutorId, tutorId, room, startsAt, startsAt.AddMinutes(60), CancelledAt: null,
            MovedToSessionId: null, LegacyViolation: false, Attendees: []);
    }

    /// <summary>A cancel, flagged the way every write flags it.</summary>
    private static BookingChange Change(GetTutorDayResponse session, DateTimeOffset changedAt, Guid? attendeeId = null) => new()
    {
        Id = Guid.NewGuid(),
        SessionId = session.Id,
        AttendeeId = attendeeId,
        Kind = ChangeKind.Cancelled,
        ChangedAt = changedAt,
        ChangedBy = CancelledBy.Family,
        AfterCutoff = Policy.IsAfterCutoff(changedAt, session.StartsAt),
    };

    private static string Lesson(SessionView s) => s.Attendees[0].LessonId!;
}
