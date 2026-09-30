using System.Net;
using BrightPath.Api.Domain;
using BrightPath.Api.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api.IntegrationTests;

/// <summary>
/// Cancel through HTTP. The shared host keeps 03-06 and 03-07 as the export, so these tests move the clock to
/// Tuesday 2026-03-24 10:00 and book their own sessions on 03-24 and 03-25, each in a room and time of its own.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class CancelAttendeeTests(BrightPathApiFactory factory) : IDisposable
{
    private readonly WebApplicationFactory<Program> _moved =
        factory.WithWebHostBuilder(b => b.UseSetting("Clock:Now", "2026-03-24T10:00:00+07:00"));

    public void Dispose() => _moved.Dispose();

    [Fact]
    public async Task A_late_family_cancel_is_charged_flagged_and_frees_the_slot()
    {
        var client = _moved.CreateClient();
        var chau = await factory.StudentId("Le Minh Chau");
        var booked = await ApiCalls.ReadSession(await ApiCalls.Book(client, "T1", "R1", "2026-03-24 10:30", 60, chau));

        var response = await ApiCalls.Cancel(client, booked.Id, booked.Attendees[0].Id, "family", "flu");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await ApiCalls.ReadSession(response);
        var attendee = Assert.Single(session.Attendees);
        Assert.Equal(AttendeeStatus.Cancelled, attendee.Status);
        Assert.Equal(CancelledBy.Family, attendee.CancelledBy);
        Assert.True(attendee.Chargeable);
        Assert.True(session.Cancelled);
        Assert.True(session.ChangedAfterCutoff);

        var cancels = session.Changes.Where(c => c.Kind == ChangeKind.Cancelled).ToList();
        Assert.Equal(2, cancels.Count);
        Assert.All(cancels, c => Assert.True(c.AfterCutoff));
        Assert.Equal("flu", cancels.Single(c => c.AttendeeId == attendee.Id).Note);
        Assert.Single(cancels, c => c.AttendeeId is null);

        var again = await ApiCalls.Book(client, "T1", "R1", "2026-03-24 10:30", 60, chau);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task The_last_of_a_pair_to_cancel_cancels_the_session()
    {
        var client = _moved.CreateClient();
        var pair = await ApiCalls.ReadSession(await ApiCalls.Book(
            client, "T2", "R2", "2026-03-25 10:00", 60,
            await factory.StudentId("Vu Ha My"), await factory.StudentId("Tran Bao Long")));

        var first = await ApiCalls.ReadSession(
            await ApiCalls.Cancel(client, pair.Id, pair.Attendees[0].Id, "centre"));
        Assert.False(first.Cancelled);
        Assert.Single(first.Changes, c => c.Kind == ChangeKind.Cancelled);

        var last = await ApiCalls.ReadSession(
            await ApiCalls.Cancel(client, pair.Id, pair.Attendees[1].Id, "family"));
        Assert.True(last.Cancelled);
        Assert.Equal(3, last.Changes.Count(c => c.Kind == ChangeKind.Cancelled));
        Assert.Single(last.Changes, c => c.Kind == ChangeKind.Cancelled && c.AttendeeId is null);
        Assert.Null(last.Changes[^1].AttendeeId); // the student goes first, then the session
    }

    [Fact]
    public async Task Cancelling_twice_is_a_409()
    {
        var client = _moved.CreateClient();
        var booked = await ApiCalls.ReadSession(await ApiCalls.Book(
            client, "T3", "R3", "2026-03-25 12:00", 60, await factory.StudentId("Do Van Kien")));
        await ApiCalls.Cancel(client, booked.Id, booked.Attendees[0].Id, "family");

        var again = await ApiCalls.Cancel(client, booked.Id, booked.Attendees[0].Id, "family");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(RuleCodes.AlreadyCancelled, Assert.Single(await ApiCalls.ReadConflicts(again)).Rule);
    }

    [Fact]
    public async Task A_lesson_that_is_already_over_cannot_be_cancelled()
    {
        // The shared host's clock is 03-06 10:00, and L018 ran at 09:00. A refused cancel writes nothing.
        var client = factory.CreateClient();
        var (session, attendee) = await Lesson(client, "L018");

        var response = await ApiCalls.Cancel(client, session, attendee, "family");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(RuleCodes.AlreadyStarted, Assert.Single(await ApiCalls.ReadConflicts(response)).Rule);
    }

    [Fact]
    public async Task An_attendee_under_another_session_is_not_found()
    {
        var client = factory.CreateClient();
        var (_, l020) = await Lesson(client, "L020");
        var (l021Session, _) = await Lesson(client, "L021");

        var response = await ApiCalls.Cancel(client, l021Session, l020, "family");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Only_family_tutor_or_centre_can_cancel()
    {
        var client = factory.CreateClient();
        var (session, attendee) = await Lesson(client, "L020");

        var response = await ApiCalls.Cancel(client, session, attendee, "teacher");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Two_cancels_of_a_pair_at_the_same_moment_still_cancel_the_session()
    {
        var client = _moved.CreateClient();
        var pair = await ApiCalls.ReadSession(await ApiCalls.Book(
            client, "T1", "R4", "2026-03-25 14:00", 60,
            await factory.StudentId("Bui An Nhien"), await factory.StudentId("Nguyen Thi Ha")));

        // The other receptionist cancels the first student, holding the session row the API locks.
        await using var other = factory.OpenDb();
        await using var transaction = await other.Database.BeginTransactionAsync();
        await other.Sessions.FromSql($"SELECT * FROM sessions WHERE id = {pair.Id} FOR UPDATE").SingleAsync();
        var first = await other.Attendees.SingleAsync(a => a.Id == pair.Attendees[0].Id);
        first.Status = AttendeeStatus.Cancelled;
        first.CancelledAt = DateTimeOffset.Parse("2026-03-24T03:00:00Z");
        first.CancelledBy = CancelledBy.Family;
        await other.SaveChangesAsync();

        var ours = ApiCalls.Cancel(client, pair.Id, pair.Attendees[1].Id, "family");
        await ApiCalls.WaitUntilBlocked(
            factory.ConnectionString,
            (type, _) => type == "Lock",
            ours,
            "The cancel did not wait for the session row");
        await transaction.CommitAsync();

        var response = await ours;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await ApiCalls.ReadSession(response)).Cancelled);
    }

    private static async Task<(Guid Session, Guid Attendee)> Lesson(HttpClient client, string lessonId)
    {
        var day = await ApiCalls.Schedule(client, "2026-03-06");
        var session = day.Sessions.Single(s => s.Attendees.Any(a => a.LessonId == lessonId));
        return (session.Id, session.Attendees.Single(a => a.LessonId == lessonId).Id);
    }
}
