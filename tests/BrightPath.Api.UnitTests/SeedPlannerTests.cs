using BrightPath.Api.Domain;
using BrightPath.Api.Seed;

namespace BrightPath.Api.UnitTests;

/// <summary>The real export, planned without a database.</summary>
public sealed class SeedPlannerTests
{
    private static readonly SeedPlan Plan = SeedPlanner.Plan(
        SeedCsv.ReadTutors(Path.Combine(SeedCsv.Directory, "tutors.csv")),
        SeedCsv.ReadLessons(Path.Combine(SeedCsv.Directory, "lessons_export.csv")),
        TestPolicy.Create());

    private static Attendee Lesson(string lessonId) => Plan.Attendees.Single(a => a.SourceLessonId == lessonId);

    private static Session SessionOf(string lessonId) => Plan.Sessions.Single(s => s.Attendees.Contains(Lesson(lessonId)));

    [Fact]
    public void Row_counts_match_the_export()
    {
        Assert.Equal(3, Plan.Tutors.Count);
        Assert.Equal(6, Plan.Students.Count);
        Assert.Equal(33, Plan.Sessions.Count);
        Assert.Equal(34, Plan.Attendees.Count());
        Assert.Equal(2, Plan.Changes.Count);
    }

    [Fact]
    public void Exam_pair_is_one_session_with_two_attendees()
    {
        Assert.Same(SessionOf("L009"), SessionOf("L010"));
        Assert.Equal(2, SessionOf("L009").Attendees.Count);
    }

    [Fact]
    public void Same_tutor_and_time_in_two_rooms_stays_two_sessions()
    {
        Assert.NotSame(SessionOf("L033"), SessionOf("L034"));
    }

    [Fact]
    public void Only_the_later_row_of_each_overlapping_pair_is_legacy()
    {
        var legacySession = Assert.Single(Plan.Sessions, s => s.LegacyViolation);
        Assert.Same(SessionOf("L034"), legacySession);

        var legacyAttendee = Assert.Single(Plan.Attendees, a => a.LegacyViolation);
        Assert.Equal("L008", legacyAttendee.SourceLessonId);
    }

    [Theory]
    [InlineData("L005", CancelledBy.Family)]
    [InlineData("L017", CancelledBy.Tutor)]
    public void Cancelled_rows_record_who_cancelled_and_cancel_the_session(string lessonId, string cancelledBy)
    {
        var attendee = Lesson(lessonId);
        Assert.Equal(AttendeeStatus.Cancelled, attendee.Status);
        Assert.Equal(cancelledBy, attendee.CancelledBy);
        Assert.False(attendee.Chargeable);
        Assert.Equal(attendee.CancelledAt, SessionOf(lessonId).CancelledAt);

        var change = Assert.Single(Plan.Changes, c => c.AttendeeId == attendee.Id);
        Assert.Equal(ChangeKind.Cancelled, change.Kind);
        Assert.Equal(cancelledBy, change.ChangedBy);
        Assert.True(change.AfterCutoff);
    }

    [Fact]
    public void No_show_keeps_its_session_active()
    {
        Assert.Equal(AttendeeStatus.NoShow, Lesson("L015").Status);
        Assert.Null(SessionOf("L015").CancelledAt);
    }

    [Fact]
    public void Local_times_are_stored_in_utc()
    {
        var session = SessionOf("L018");
        Assert.Equal(new DateTimeOffset(2026, 3, 6, 2, 0, 0, TimeSpan.Zero), session.StartsAt);
        Assert.Equal(TimeSpan.Zero, session.StartsAt.Offset);
        Assert.Equal(new DateTimeOffset(2026, 3, 6, 3, 0, 0, TimeSpan.Zero), session.EndsAt);
    }
}

public sealed class BookingPolicyTests
{
    private static readonly BookingPolicy Policy = TestPolicy.Create();
    private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

    // Friday 2026-03-06 14:00 local.
    private static readonly DateTimeOffset Friday2pm = new(2026, 3, 6, 14, 0, 0, Vn);

    [Theory]
    [InlineData(10, 1, true)] // 3h59m before
    [InlineData(10, 0, false)] // exactly 4h before
    [InlineData(9, 0, false)] // 5h before
    public void Family_cancelling_inside_four_hours_is_chargeable(int hour, int minute, bool expected)
    {
        var cancelledAt = new DateTimeOffset(2026, 3, 6, hour, minute, 0, Vn);
        Assert.Equal(expected, Policy.IsChargeable(CancelledBy.Family, cancelledAt, Friday2pm));
    }

    [Theory]
    [InlineData(CancelledBy.Tutor)]
    [InlineData(CancelledBy.Centre)]
    [InlineData(null)]
    public void Only_a_family_cancellation_is_chargeable(string? cancelledBy)
    {
        Assert.False(Policy.IsChargeable(cancelledBy, Friday2pm.AddHours(-1), Friday2pm));
    }

    [Theory]
    [InlineData(5, 15, 59, false)] // Thursday 15:59, before the cut-off
    [InlineData(5, 16, 0, true)] // Thursday 16:00, the schedule is final
    [InlineData(6, 8, 0, true)] // same day
    public void Change_is_after_cutoff_from_16_00_the_day_before(int day, int hour, int minute, bool expected)
    {
        var changedAt = new DateTimeOffset(2026, 3, day, hour, minute, 0, Vn);
        Assert.Equal(expected, Policy.IsAfterCutoff(changedAt, Friday2pm));
    }

    [Fact]
    public void Tuesday_lesson_changed_on_monday_evening_is_after_cutoff()
    {
        var tuesday9am = new DateTimeOffset(2026, 3, 10, 9, 0, 0, Vn);
        var mondayEvening = new DateTimeOffset(2026, 3, 9, 18, 0, 0, Vn);
        Assert.True(Policy.IsAfterCutoff(mondayEvening, tuesday9am));
    }

    [Theory]
    [InlineData("family cancelled", CancelledBy.Family)]
    [InlineData("Tutor sick", CancelledBy.Tutor)]
    [InlineData("centre closed; family told", CancelledBy.Centre)]
    [InlineData("added by phone", null)]
    [InlineData(null, null)]
    public void Who_cancelled_is_read_from_the_note(string? note, string? expected)
    {
        Assert.Equal(expected, CancelledBy.FromNote(note));
    }
}

internal static class TestPolicy
{
    public static BookingPolicy Create() => new(new BookingPolicyOptions
    {
        TimeZone = "Asia/Ho_Chi_Minh",
        CutoffLocalTime = new TimeOnly(16, 0),
        LateCancellationWindow = TimeSpan.FromHours(4),
        MaxSessionsPerTutorPerDay = 6,
        MaxAttendeesPerSession = 2,
        ClosedDays = [DayOfWeek.Monday],
        OpensAt = new TimeOnly(9, 0),
        ClosesAt = new TimeOnly(21, 30),
    });
}
