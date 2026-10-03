using BrightPath.Api.IntegrationTests.Infrastructure;
using BrightPath.Application.Sessions;
using BrightPath.Domain;
using Microsoft.AspNetCore.Hosting;

namespace BrightPath.Api.IntegrationTests;

/// <summary>The day's sessions over HTTP, with the clock read from config. No other test books on 03-06 or 03-07.</summary>
[Collection(ApiCollection.Name)]
public sealed class DaySessionsEndpointTests(BrightPathApiFactory factory)
{
    [Fact]
    public async Task Today_is_the_pinned_day()
    {
        var day = await ApiCalls.DaySessions(factory.CreateClient());

        Assert.Equal(new DateOnly(2026, 3, 6), day.Date);
        Assert.Equal(DateTimeOffset.Parse("2026-03-06T10:00:00+07:00"), day.Now);
        Assert.Equal(TimeSpan.FromHours(7), day.Now.Offset);
        Assert.Equal(10, day.Sessions.Count);
        Assert.Equal(7, day.Sessions.Count(s => s.RoomId == "R1"));
        Assert.DoesNotContain(day.Sessions, s => s.RoomId is "R4" or "R5" or "R6");
    }

    [Fact]
    public async Task Moving_the_clock_in_config_moves_today()
    {
        using var moved = factory.WithWebHostBuilder(b => b.UseSetting("Clock:Now", "2026-03-07T10:00:00+07:00"));

        var day = await ApiCalls.DaySessions(moved.CreateClient());

        Assert.Equal(new DateOnly(2026, 3, 7), day.Date);
        Assert.Equal(DateTimeOffset.Parse("2026-03-07T10:00:00+07:00"), day.Now);
        Assert.Contains(day.Sessions, s => s.Attendees.Any(a => a.LessonId == "L028"));
    }
}
