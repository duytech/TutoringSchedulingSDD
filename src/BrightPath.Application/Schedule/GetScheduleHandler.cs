using BrightPath.Application.Abstractions;
using BrightPath.Domain;

namespace BrightPath.Application.Schedule;

/// <summary>One day's schedule.</summary>
public sealed class GetScheduleHandler(IScheduleReader reader, BookingPolicy policy, TimeProvider clock)
{
    /// <summary>The default date is today on the clock.</summary>
    public async Task<ScheduleDayView> HandleAsync(DateOnly? date, CancellationToken ct)
    {
        var utcNow = clock.GetUtcNow();
        var localDate = date ?? policy.LocalDate(utcNow);
        var daySessions = await reader.DaySessionsAsync(localDate, tutorId: null, ct);
        var sessionIds = daySessions.Select(s => s.Id).ToList();
        var targetIds = daySessions.Select(s => s.MovedToSessionId).OfType<Guid>().ToList();
        var bookingChanges = await reader.ChangesOfAsync(sessionIds, ct);
        var moveTargets = await reader.MoveTargetsAsync(targetIds, ct);

        return ScheduleDay.Build(localDate, utcNow, daySessions, bookingChanges, policy, moveTargets);
    }
}
