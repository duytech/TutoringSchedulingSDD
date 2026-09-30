using System.Net;
using BrightPath.Api.IntegrationTests.Infrastructure;
using BrightPath.Domain;

namespace BrightPath.Api.IntegrationTests;

/// <summary>
/// One test per rule, through HTTP on a real Postgres. The rules themselves are proven in BookingCheckTests;
/// these prove the endpoint runs them. Each test books on a date of its own, so the tests cannot see each other.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class SessionConflictTests(BrightPathApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task A_student_cannot_be_in_two_sessions_at_once()
    {
        var chau = await factory.StudentId("Le Minh Chau");

        var first = await ApiCalls.Book(_client, "T1", "R1", "2026-03-11 09:00", 60, chau);
        var second = await ApiCalls.Book(_client, "T2", "R2", "2026-03-11 09:00", 60, chau);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var conflict = await OnlyConflict(second, RuleCodes.StudentOverlap);
        Assert.Contains("Le Minh Chau", conflict.Message);
    }

    [Fact]
    public async Task A_room_cannot_hold_two_sessions_at_once()
    {
        var first = await ApiCalls.Book(_client, "T1", "R1", "2026-03-12 09:00", 60, await factory.StudentId("Le Minh Chau"));
        var second = await ApiCalls.Book(_client, "T2", "R1", "2026-03-12 09:30", 60, await factory.StudentId("Tran Bao Long"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await OnlyConflict(second, RuleCodes.RoomOverlap);
    }

    [Fact]
    public async Task A_tutor_cannot_be_in_two_rooms_at_once()
    {
        var first = await ApiCalls.Book(_client, "T1", "R1", "2026-03-13 09:00", 60, await factory.StudentId("Le Minh Chau"));
        var second = await ApiCalls.Book(_client, "T1", "R2", "2026-03-13 09:00", 60, await factory.StudentId("Tran Bao Long"));

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        await OnlyConflict(second, RuleCodes.TutorOverlap);
    }

    [Fact]
    public async Task A_tutor_already_over_the_daily_load_gets_no_more_sessions()
    {
        // T1 has 7 seeded sessions on 03-06. R4 and Do Van Kien are free at 14:30.
        var response = await ApiCalls.Book(_client, "T1", "R4", "2026-03-06 14:30", 60, await factory.StudentId("Do Van Kien"));

        var conflict = await OnlyConflict(response, RuleCodes.TutorLoad);
        Assert.Equal(8, conflict.SessionIds.Count);
    }

    [Fact]
    public async Task Nothing_is_booked_on_a_Monday()
    {
        var response = await ApiCalls.Book(_client, "T2", "R2", "2026-03-16 10:00", 60, await factory.StudentId("Le Minh Chau"));

        await OnlyConflict(response, RuleCodes.ClosedDay);
    }

    [Fact]
    public async Task An_exam_pair_is_one_session_with_two_attendees()
    {
        var ha = await factory.StudentId("Nguyen Thi Ha");
        var kien = await factory.StudentId("Do Van Kien");

        var response = await ApiCalls.Book(_client, "T3", "R5", "2026-03-18 14:00", 90, ha, kien);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ApiCalls.ReadSession(response);
        Assert.Equal($"/api/sessions/{created.Id}", response.Headers.Location?.OriginalString);

        var session = await ApiCalls.ReadSession(await _client.GetAsync(response.Headers.Location));
        Assert.Equal(new[] { ha, kien }.Order(), session.Attendees.Select(a => a.StudentId).Order());
        Assert.All(session.Attendees, a => Assert.Equal(AttendeeStatus.Booked, a.Status));
        var change = Assert.Single(session.Changes);
        Assert.Equal(ChangeKind.Created, change.Kind);
        Assert.Equal(CancelledBy.Centre, change.ChangedBy);
        Assert.False(change.AfterCutoff);

        var day = await ApiCalls.Schedule(_client, "2026-03-18");
        Assert.Equal([created.Id], day.Rooms.Single(r => r.Id == "R5").SessionIds);
        Assert.Equal([created.Id], day.Tutors.Single(t => t.Id == "T3").SessionIds);
    }

    private static async Task<Conflict> OnlyConflict(HttpResponseMessage response, string rule)
    {
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var conflict = Assert.Single(await ApiCalls.ReadConflicts(response));
        Assert.Equal(rule, conflict.Rule);
        return conflict;
    }
}
