using BrightPath.Api.IntegrationTests.Infrastructure;
using BrightPath.Application.Schedule;
using BrightPath.Domain;
using Microsoft.AspNetCore.Hosting;

namespace BrightPath.Api.IntegrationTests;

/// <summary>The schedule over HTTP, with the clock read from config. No other test books on 03-06 or 03-07.</summary>
[Collection(ApiCollection.Name)]
public sealed class ScheduleEndpointTests(BrightPathApiFactory factory)
{
    [Fact]
    public async Task Today_is_the_pinned_day()
    {
        var day = await ApiCalls.Schedule(factory.CreateClient());

        Assert.Equal(new DateOnly(2026, 3, 6), day.Date);
        Assert.Equal(DateTimeOffset.Parse("2026-03-06T10:00:00+07:00"), day.Now);
        Assert.Equal(TimeSpan.FromHours(7), day.Now.Offset);
        Assert.Equal(10, day.Sessions.Count);
        Assert.Equal(7, day.Rooms.Single(r => r.Id == "R1").SessionIds.Count);
        Assert.All(day.Rooms.Where(r => r.Id is "R4" or "R5" or "R6"), r => Assert.Empty(r.SessionIds));

        var past = day.Sessions.Where(s => s.State == SessionState.Past).SelectMany(s => s.Attendees).Select(a => a.LessonId);
        Assert.Equal(["L018", "L019"], past.Order());
        Assert.Equal(8, day.Sessions.Count(s => s.State == SessionState.Upcoming));
    }

    [Fact]
    public async Task Moving_the_clock_in_config_moves_today()
    {
        using var moved = factory.WithWebHostBuilder(b => b.UseSetting("Clock:Now", "2026-03-07T10:00:00+07:00"));

        var day = await ApiCalls.Schedule(moved.CreateClient());

        Assert.Equal(new DateOnly(2026, 3, 7), day.Date);
        Assert.Equal(SessionState.InProgress, StateOf(day, "L028"));
        Assert.Equal(SessionState.Upcoming, StateOf(day, "L029"));
        Assert.Equal(SessionState.Upcoming, StateOf(day, "L030"));
    }

    private static string StateOf(ScheduleDayView day, string lessonId) =>
        day.Sessions.Single(s => s.Attendees.Any(a => a.LessonId == lessonId)).State;
}
