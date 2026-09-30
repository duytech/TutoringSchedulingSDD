using BrightPath.Application.Abstractions;
using BrightPath.Domain;

namespace BrightPath.Application.Schedule;

/// <summary>One day's schedule.</summary>
public sealed class GetScheduleHandler(IScheduleReader reader, BookingPolicy policy, TimeProvider clock)
{
    /// <summary>The default date is today on the clock.</summary>
    public async Task<ScheduleDayView> HandleAsync(DateOnly? date, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var day = date ?? policy.LocalDate(now);
        var sessions = await reader.DaySessionsAsync(day, tutorId: null, ct);
        var changes = await reader.ChangesOfAsync(sessions, ct);
        var moveTargets = await reader.MoveTargetsAsync(sessions, ct);

        return ScheduleDay.Build(day, now, sessions, changes, policy, moveTargets);
    }
}
