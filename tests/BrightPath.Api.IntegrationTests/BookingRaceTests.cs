using System.Net;
using BrightPath.Api.IntegrationTests.Infrastructure;
using BrightPath.Domain;
using BrightPath.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api.IntegrationTests;

/// <summary>
/// Two receptionists booking at the same moment. Parallel requests usually run one after the other, so they would
/// pass even without the guards. Instead the test holds a transaction of its own open, lets one API request run into
/// it, and only then commits. Each test fails if its guard is removed.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class BookingRaceTests(BrightPathApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task A_room_taken_between_the_check_and_the_insert_is_a_409_not_a_500()
    {
        await using var other = factory.OpenDb();
        await using var transaction = await other.Database.BeginTransactionAsync();
        var theirs = NewSession("T1", "R6", "2026-03-19 10:00");
        other.Sessions.Add(theirs);
        await other.SaveChangesAsync();

        // T2, not T1: a different tutor-day lock, so the API reads the day, sees nothing committed, and passes.
        var ours = ApiCalls.Book(_client, "T2", "R6", "2026-03-19 10:00", 60, await factory.StudentId("Tran Bao Long"));
        await ApiCalls.WaitUntilBlocked(
            factory.ConnectionString,
            (type, _) => type == "Lock",
            ours,
            "The insert did not wait on the room constraint");
        await transaction.CommitAsync();

        var response = await ours;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var conflict = Assert.Single(await ApiCalls.ReadConflicts(response));
        Assert.Equal(RuleCodes.RoomOverlap, conflict.Rule);
        Assert.Equal("R6 was booked by someone else at the same moment.", conflict.Message);
        Assert.Single(conflict.SessionIds);
        Assert.NotEqual(theirs.Id, conflict.SessionIds[0]);
        Assert.Empty(conflict.LessonIds);

        var day = await ApiCalls.Schedule(_client, "2026-03-19");
        Assert.Equal([theirs.Id], day.Sessions.Where(s => s.RoomId == "R6").Select(s => s.Id));
    }

    [Fact]
    public async Task Two_bookings_for_the_last_slot_of_a_tutors_day_cannot_both_get_it()
    {
        var my = await factory.StudentId("Vu Ha My");
        foreach (var start in new[] { "09:00", "10:30", "12:00", "13:30", "15:00" })
        {
            var booked = await ApiCalls.Book(_client, "T2", "R1", $"2026-03-17 {start}", 60, my);
            Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        }

        // The other receptionist takes the 6th slot, holding the same lock the API takes.
        await using var other = factory.OpenDb();
        await using var transaction = await other.Database.BeginTransactionAsync();
        var key = BookingLocks.TutorDay("T2", new DateOnly(2026, 3, 17));
        await other.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))");
        other.Sessions.Add(NewSession("T2", "R2", "2026-03-17 16:30"));
        await other.SaveChangesAsync();

        var ours = ApiCalls.Book(_client, "T2", "R3", "2026-03-17 18:00", 60, my);
        await ApiCalls.WaitUntilBlocked(
            factory.ConnectionString,
            (type, name) => type == "Lock" && name == "advisory",
            ours,
            "The booking did not wait for the tutor-day lock");
        await transaction.CommitAsync();

        var response = await ours;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var conflict = Assert.Single(await ApiCalls.ReadConflicts(response));
        Assert.Equal(RuleCodes.TutorLoad, conflict.Rule);
    }

    private static Session NewSession(string tutorId, string roomId, string localStart)
    {
        var start = DateTimeOffset.Parse($"{localStart.Replace(' ', 'T')}:00+07:00").ToUniversalTime();
        return new Session
        {
            Id = Guid.CreateVersion7(),
            TutorId = tutorId,
            RoomId = roomId,
            StartsAt = start,
            EndsAt = start.AddMinutes(60),
        };
    }
}
