using System.Text.Json;
using BrightPath.Api.UnitTests.Infrastructure;
using BrightPath.Application.Sessions;
using BrightPath.Application.Tutors;
using BrightPath.Common;
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
        Assert.Empty(Late(sheet));
    }

    [Fact]
    public void A_tutor_cancellation_on_the_day_is_flagged_on_its_session()
    {
        var sheet = Export("T3", new DateOnly(2026, 3, 5));

        Assert.Equal(["L014", "L017"], sheet.Sessions.Select(Lesson));
        Assert.NotNull(sheet.Sessions[1].CancelledAt);
        var (session, change) = Assert.Single(Late(sheet));
        Assert.Equal(sheet.Sessions[1].Id, session.Id);
        Assert.Equal(ChangeKind.Cancelled, change.Kind);
        Assert.Equal("Do Van Kien", session.Attendees.Single(a => a.Id == change.AttendeeId).StudentName);
        Assert.Equal(CancelledBy.Tutor, change.ChangedBy);
        Assert.Equal(DateTimeOffset.Parse("2026-03-05T14:40:00+07:00"), change.ChangedAt);
        Assert.Equal("tutor sick", change.Note);
    }

    [Fact]
    public void A_family_cancellation_is_flagged_too()
    {
        var sheet = Export("T2", new DateOnly(2026, 3, 3));

        Assert.Equal(["L002", "L005"], sheet.Sessions.Select(Lesson));
        var (_, change) = Assert.Single(Late(sheet));
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
    }

    [Theory]
    [InlineData("2026-03-05T16:00:00+07:00", true)]
    [InlineData("2026-03-05T15:59:59+07:00", false)]
    public void Final_flips_at_the_cutoff(string now, bool final) =>
        Assert.Equal(final, EmptyDay(DateTimeOffset.Parse(now)).Final);

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
            var day = DaySessions.Build(Seed.DaySessionsOn(date, Policy), Seed.Changes, Policy, moveTargets);
            foreach (var tutor in Seed.Tutors)
            {
                var sheet = TutorDaySheet.Build(
                    tutor, date, PinnedNow, Seed.TutorDayOn(tutor.Id, date, Policy), Seed.Changes, Policy, moveTargets);
                var inDay = day.Where(s => s.TutorId == tutor.Id);

                Assert.Equal(JsonSerializer.Serialize(inDay), JsonSerializer.Serialize(sheet.Sessions));
            }
        }
    }

    /// <summary>The changes made after the cut-off, each with its session.</summary>
    private static List<(SessionView Session, BookingChangeView Change)> Late(TutorDaySheetView sheet) =>
        sheet.Sessions.SelectMany(s => s.Changes.Where(c => c.AfterCutoff).Select(c => (s, c))).ToList();

    private static TutorDaySheetView Export(string tutorId, DateOnly date) =>
        TutorDaySheet.Build(
            Seed.Tutors.Single(t => t.Id == tutorId),
            date,
            PinnedNow,
            Seed.TutorDayOn(tutorId, date, Policy),
            Seed.Changes,
            Policy);

    private static TutorDaySheetView EmptyDay(DateTimeOffset now) => TutorDaySheet.Build(T1, Friday, now, [], [], Policy);

    private static string Lesson(SessionView s) => s.Attendees[0].LessonId!;
}
