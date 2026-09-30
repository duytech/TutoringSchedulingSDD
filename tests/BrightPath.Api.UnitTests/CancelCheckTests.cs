using BrightPath.Api.Domain;
using BrightPath.Api.Seed;

namespace BrightPath.Api.UnitTests;

public sealed class CancelCheckTests
{
    private static readonly BookingPolicy Policy = TestPolicy.Create();
    private static readonly DateTimeOffset PinnedNow = DateTimeOffset.Parse("2026-03-06T10:00:00+07:00");
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-03-24T14:00:00+07:00");
    private static readonly SeedPlan Plan = SeedPlanner.Plan(
        SeedCsv.ReadTutors(Path.Combine(SeedCsv.Directory, "tutors.csv")),
        SeedCsv.ReadLessons(Path.Combine(SeedCsv.Directory, "lessons_export.csv")),
        Policy);

    // Rows of the expected-results table in the phase 13 spec, checked on the real export.
    [Theory]
    [InlineData("L020", "family", true, true)] // row 1: 30 min before, after the cut-off
    [InlineData("L024", "family", false, true)] // row 2: 6 h before, but after the cut-off
    [InlineData("L025", "tutor", false, true)] // row 3: only a family pays
    [InlineData("L028", "family", false, false)] // row 4: tomorrow, before today's 16:00
    public void Cancel_on_the_export_is_charged_and_flagged_as_the_spec_says(
        string lessonId, string cancelledBy, bool chargeable, bool afterCutoff)
    {
        var (session, attendeeId) = FromExport(lessonId);

        var decision = CancelCheck.Decide(session, attendeeId, cancelledBy, PinnedNow, Policy);

        Assert.Empty(decision.Conflicts);
        Assert.Equal(chargeable, decision.Chargeable);
        Assert.Equal(afterCutoff, decision.AfterCutoff);
        Assert.True(decision.CancelsSession);
    }

    [Fact]
    public void A_lesson_that_is_already_over_cannot_be_cancelled()
    {
        var (session, attendeeId) = FromExport("L018");

        var conflict = Assert.Single(CancelCheck.Decide(session, attendeeId, "family", PinnedNow, Policy).Conflicts);

        Assert.Equal(RuleCodes.AlreadyStarted, conflict.Rule);
        Assert.Equal(["L018"], conflict.LessonIds);
        Assert.Equal("The session started at 2026-03-06 09:00, before now (2026-03-06 10:00).", conflict.Message);
    }

    [Fact]
    public void An_old_cancellation_gets_both_reasons()
    {
        var (session, attendeeId) = FromExport("L005");

        var conflicts = CancelCheck.Decide(session, attendeeId, "family", PinnedNow, Policy).Conflicts;

        Assert.Equal([RuleCodes.AlreadyStarted, RuleCodes.AlreadyCancelled], conflicts.Select(c => c.Rule));
        Assert.Equal("Vu Ha My was already cancelled at 2026-03-03 08:15 by family.", conflicts[1].Message);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void A_session_counts_as_started_from_its_first_minute(int minutesBefore, bool refused)
    {
        var session = Session(Start, Booked("A"));

        var decision = CancelCheck.Decide(session, session.Attendees[0].Id, "centre", Start.AddMinutes(-minutesBefore), Policy);

        Assert.Equal(refused, decision.Conflicts.Any(c => c.Rule == RuleCodes.AlreadyStarted));
    }

    [Theory]
    [InlineData(240, false)]
    [InlineData(239, true)]
    public void A_family_is_charged_only_inside_four_hours(int minutesBefore, bool chargeable)
    {
        var session = Session(Start, Booked("A"));

        var decision = CancelCheck.Decide(session, session.Attendees[0].Id, "family", Start.AddMinutes(-minutesBefore), Policy);

        Assert.Equal(chargeable, decision.Chargeable);
    }

    [Fact]
    public void The_first_of_a_pair_leaves_the_session_and_the_last_one_cancels_it()
    {
        var pair = Session(Start, Booked("A"), Booked("B"));
        var now = Start.AddDays(-2);

        Assert.False(CancelCheck.Decide(pair, pair.Attendees[0].Id, "family", now, Policy).CancelsSession);

        var oneLeft = pair with
        {
            Attendees = [pair.Attendees[0] with { Status = AttendeeStatus.Cancelled, CancelledAt = now }, pair.Attendees[1]],
        };
        Assert.True(CancelCheck.Decide(oneLeft, oneLeft.Attendees[1].Id, "family", now, Policy).CancelsSession);
    }

    [Theory]
    [InlineData(null, "A was already cancelled at 2026-03-24 08:00.")]
    [InlineData("tutor", "A was already cancelled at 2026-03-24 08:00 by tutor.")]
    public void Already_cancelled_says_by_whom_when_it_is_known(string? by, string message)
    {
        var cancelledAt = DateTimeOffset.Parse("2026-03-24T08:00:00+07:00");
        var session = Session(Start, Booked("A") with
        {
            Status = AttendeeStatus.Cancelled, CancelledAt = cancelledAt, CancelledBy = by,
        });

        var conflict = Assert.Single(CancelCheck.Decide(session, session.Attendees[0].Id, "family", cancelledAt, Policy).Conflicts);

        Assert.Equal(message, conflict.Message);
    }

    private static CancelSession Session(DateTimeOffset start, params CancelAttendee[] attendees) =>
        new(Guid.NewGuid(), start.ToUniversalTime(), attendees);

    private static CancelAttendee Booked(string name) =>
        new(Guid.NewGuid(), name, AttendeeStatus.Booked, null, null, null);

    private static (CancelSession Session, Guid AttendeeId) FromExport(string lessonId)
    {
        var session = Plan.Sessions.Single(s => s.Attendees.Any(a => a.SourceLessonId == lessonId));
        var names = Plan.Students.ToDictionary(s => s.Id, s => s.Name);
        return (
            new CancelSession(
                session.Id,
                session.StartsAt,
                session.Attendees
                    .Select(a => new CancelAttendee(
                        a.Id, names[a.StudentId], a.Status, a.CancelledAt, a.CancelledBy, a.SourceLessonId))
                    .ToList()),
            session.Attendees.Single(a => a.SourceLessonId == lessonId).Id);
    }
}
