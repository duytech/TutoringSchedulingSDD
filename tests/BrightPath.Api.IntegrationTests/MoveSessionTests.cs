using System.Net;
using BrightPath.Api.IntegrationTests.Infrastructure;
using BrightPath.Application.Sessions;
using BrightPath.Domain;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api.IntegrationTests;

/// <summary>
/// Move through HTTP. Like the cancel tests, the clock is moved to Tuesday 2026-03-24 10:00 so the export's days
/// stay untouched, and each test books its own sessions on 03-26 and 03-27, in rooms and times of its own.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class MoveSessionTests(BrightPathApiFactory factory) : IDisposable
{
    private readonly WebApplicationFactory<Program> _moved =
        factory.WithWebHostBuilder(b => b.UseSetting("Clock:Now", "2026-03-24T10:00:00+07:00"));

    public void Dispose() => _moved.Dispose();

    [Fact]
    public async Task A_move_cancels_the_old_session_links_it_and_takes_only_the_booked_students()
    {
        var client = _moved.CreateClient();
        var my = await factory.StudentId("Vu Ha My");
        var long_ = await factory.StudentId("Tran Bao Long");
        var pair = await ApiCalls.ReadSession(await ApiCalls.Book(client, "T1", "R1", "2026-03-26 10:00", 60, my, long_));
        await ApiCalls.Cancel(client, pair.Id, pair.Attendees.Single(a => a.StudentId == my).Id, "centre");

        var response = await ApiCalls.Move(client, pair.Id, "2026-03-26 12:00", "family", "R2");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var moved = await ApiCalls.ReadSession(response);
        Assert.Equal($"/api/sessions/{moved.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("R2", moved.RoomId);
        Assert.Equal([long_], moved.Attendees.Select(a => a.StudentId));
        var arrived = Assert.Single(moved.Changes);
        Assert.Equal(ChangeKind.Moved, arrived.Kind);
        Assert.Equal("from 2026-03-26 10:00 in R1", arrived.Note);

        var old = await ApiCalls.ReadSession(await client.GetAsync($"/api/sessions/{pair.Id}"));
        Assert.NotNull(old.CancelledAt);
        Assert.Equal(moved.Id, old.MovedToSessionId);
        Assert.Equal(new MovedToView(moved.Id, DateTimeOffset.Parse("2026-03-26T12:00:00+07:00"), "R2"), old.MovedTo);
        Assert.All(old.Attendees, a => Assert.Equal(AttendeeStatus.Cancelled, a.Status));
        Assert.All(old.Attendees, a => Assert.False(a.Chargeable));
        Assert.Equal("to 2026-03-26 12:00 in R2", old.Changes.Single(c => c.Kind == ChangeKind.Moved).Note);
    }

    [Fact]
    public async Task A_move_can_overlap_its_own_old_slot()
    {
        var client = _moved.CreateClient();
        var booked = await ApiCalls.ReadSession(await ApiCalls.Book(
            client, "T2", "R3", "2026-03-26 14:00", 90, await factory.StudentId("Le Minh Chau")));

        var response = await ApiCalls.Move(client, booked.Id, "2026-03-26 14:30", "centre");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_move_into_a_taken_room_is_refused_and_changes_nothing()
    {
        var client = _moved.CreateClient();
        await ApiCalls.Book(client, "T3", "R4", "2026-03-27 09:00", 60, await factory.StudentId("Do Van Kien"));
        var mine = await ApiCalls.ReadSession(await ApiCalls.Book(
            client, "T1", "R5", "2026-03-27 09:00", 60, await factory.StudentId("Nguyen Thi Ha")));

        var response = await ApiCalls.Move(client, mine.Id, "2026-03-27 09:00", "centre", "R4");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(RuleCodes.RoomOverlap, Assert.Single(await ApiCalls.ReadConflicts(response)).Rule);
        var still = await ApiCalls.ReadSession(await client.GetAsync($"/api/sessions/{mine.Id}"));
        Assert.Null(still.CancelledAt);
        Assert.Null(still.MovedTo);
    }

    [Fact]
    public async Task A_session_already_moved_cannot_be_moved_again()
    {
        var client = _moved.CreateClient();
        var booked = await ApiCalls.ReadSession(await ApiCalls.Book(
            client, "T3", "R5", "2026-03-27 13:00", 60, await factory.StudentId("Bui An Nhien")));
        await ApiCalls.Move(client, booked.Id, "2026-03-27 15:00", "centre");

        var again = await ApiCalls.Move(client, booked.Id, "2026-03-27 17:00", "centre");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(RuleCodes.AlreadyCancelled, Assert.Single(await ApiCalls.ReadConflicts(again)).Rule);
    }

    [Fact]
    public async Task The_day_shows_where_a_session_moved_to_on_another_day()
    {
        var client = _moved.CreateClient();
        var booked = await ApiCalls.ReadSession(await ApiCalls.Book(
            client, "T1", "R6", "2026-03-26 16:00", 60, await factory.StudentId("Bui An Nhien")));
        var moved = await ApiCalls.ReadSession(await ApiCalls.Move(client, booked.Id, "2026-03-27 16:00", "tutor"));

        var day = await ApiCalls.DaySessions(client, "2026-03-26");

        var old = day.Sessions.Single(s => s.Id == booked.Id);
        Assert.Equal(new MovedToView(moved.Id, DateTimeOffset.Parse("2026-03-27T16:00:00+07:00"), "R6"), old.MovedTo);
        Assert.Equal(TimeSpan.FromHours(7), old.MovedTo!.StartsAt.Offset);
    }

    [Fact]
    public async Task A_move_waits_for_a_cancel_of_the_same_session_and_then_refuses()
    {
        var client = _moved.CreateClient();
        var booked = await ApiCalls.ReadSession(await ApiCalls.Book(
            client, "T2", "R6", "2026-03-27 10:00", 60, await factory.StudentId("Vu Ha My")));

        // The other receptionist cancels the session, holding the row the API locks.
        await using var other = factory.OpenDb();
        await using var transaction = await other.Database.BeginTransactionAsync();
        var session = await other.Sessions.FromSql($"SELECT * FROM sessions WHERE id = {booked.Id} FOR UPDATE").SingleAsync();
        var attendee = await other.Attendees.SingleAsync(a => a.SessionId == booked.Id);
        var cancelledAt = DateTimeOffset.Parse("2026-03-24T03:00:00Z");
        session.CancelledAt = cancelledAt;
        attendee.Status = AttendeeStatus.Cancelled;
        attendee.CancelledAt = cancelledAt;
        attendee.CancelledBy = CancelledBy.Family;
        await other.SaveChangesAsync();

        var ours = ApiCalls.Move(client, booked.Id, "2026-03-27 11:00", "centre");
        await ApiCalls.WaitUntilBlocked(
            factory.ConnectionString,
            (type, _) => type == "Lock",
            ours,
            "The move did not wait for the session row");
        await transaction.CommitAsync();

        var response = await ours;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(RuleCodes.AlreadyCancelled, Assert.Single(await ApiCalls.ReadConflicts(response)).Rule);
    }
}
