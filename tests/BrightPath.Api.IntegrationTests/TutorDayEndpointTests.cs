using System.Net;
using BrightPath.Api.IntegrationTests.Infrastructure;
using BrightPath.Domain;
using Microsoft.AspNetCore.Mvc.Testing;

namespace BrightPath.Api.IntegrationTests;

/// <summary>
/// The tutor day over HTTP. The pinned day is only read. The writes happen with the clock moved to Wednesday
/// 2026-04-01, on 04-02 and 04-03, which no other test books on.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class TutorDayEndpointTests(BrightPathApiFactory factory) : IDisposable
{
    private readonly WebApplicationFactory<Program> _morning =
        factory.WithWebHostBuilder(b => b.UseSetting("Clock:Now", "2026-04-01T10:00:00+07:00"));

    // 04-02's cut-off is 04-01 16:00, so a change from here on is one the tutor was not told about.
    private readonly WebApplicationFactory<Program> _evening =
        factory.WithWebHostBuilder(b => b.UseSetting("Clock:Now", "2026-04-01T17:00:00+07:00"));

    public void Dispose()
    {
        _morning.Dispose();
        _evening.Dispose();
    }

    [Fact]
    public async Task No_date_is_the_pinned_day()
    {
        var sheet = await ApiCalls.TutorDay(factory.CreateClient(), "T1");

        Assert.Equal(new DateOnly(2026, 3, 6), sheet.Date);
        Assert.Equal(DateTimeOffset.Parse("2026-03-05T16:00:00+07:00"), sheet.Cutoff);
        Assert.True(sheet.Final);
        Assert.Equal(7, sheet.Sessions.Count);
        Assert.All(sheet.Sessions, s => Assert.Equal("T1", s.TutorId));
        Assert.DoesNotContain(sheet.Sessions.SelectMany(s => s.Changes), c => c.AfterCutoff);
    }

    [Fact]
    public async Task An_unknown_tutor_is_404_and_a_bad_date_is_400()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await ApiCalls.GetTutorDay(client, "T9", "2026-03-06")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await ApiCalls.GetTutorDay(client, "T1", "2026-13-01")).StatusCode);
    }

    [Fact]
    public async Task A_cancel_after_the_cutoff_is_flagged_student_first()
    {
        var booked = await ApiCalls.ReadSession(await ApiCalls.Book(
            _morning.CreateClient(), "T2", "R6", "2026-04-02 14:00", 60, await factory.StudentId("Vu Ha My")));
        var evening = _evening.CreateClient();

        await ApiCalls.Cancel(evening, booked.Id, booked.Attendees[0].Id, "family", "sick");
        var sheet = await ApiCalls.TutorDay(evening, "T2", "2026-04-02");

        var session = Assert.Single(sheet.Sessions);
        Assert.NotNull(session.CancelledAt);
        var late = session.Changes.Where(c => c.AfterCutoff).ToList();
        Assert.Equal([booked.Attendees[0].Id, (Guid?)null], late.Select(c => c.AttendeeId));
        Assert.All(late, c => Assert.Equal(ChangeKind.Cancelled, c.Kind));
        Assert.Equal("sick", late[0].Note);
    }

    [Fact]
    public async Task A_move_to_the_next_day_shows_on_both_days()
    {
        var client = _morning.CreateClient();
        var booked = await ApiCalls.ReadSession(await ApiCalls.Book(
            client, "T3", "R6", "2026-04-02 09:00", 60, await factory.StudentId("Do Van Kien")));
        var moved = await ApiCalls.ReadSession(await ApiCalls.Move(client, booked.Id, "2026-04-03 09:00", "family"));

        var before = await ApiCalls.TutorDay(client, "T3", "2026-04-02");
        var after = await ApiCalls.TutorDay(client, "T3", "2026-04-03");

        var old = Assert.Single(before.Sessions);
        Assert.Equal(moved.Id, old.MovedTo?.Id);
        Assert.DoesNotContain(old.Changes, c => c.AfterCutoff);
        var arrived = Assert.Single(after.Sessions);
        Assert.Equal(moved.Id, arrived.Id);
        Assert.Contains(arrived.Changes, c => c is { Kind: ChangeKind.Moved, Note: "from 2026-04-02 09:00 in R6" });
        Assert.False(after.Final);
    }
}
