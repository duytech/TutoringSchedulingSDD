using BrightPath.Application.Abstractions;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

/// <summary>One day's sessions.</summary>
public sealed class GetDaySessionsHandler(ISessionReader reader, BookingPolicy policy, TimeProvider clock)
{
    /// <summary>The default date is today on the clock.</summary>
    public async Task<DaySessionsView> HandleAsync(DateOnly? date, CancellationToken ct)
    {
        var utcNow = clock.GetUtcNow();
        var localDate = date ?? policy.LocalDate(utcNow);
        var sessions = await reader.GetDaySessionsAsync(localDate, ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var targetIds = sessions.Select(s => s.MovedToSessionId).OfType<Guid>().ToList();
        var bookingChanges = await reader.ChangesOfAsync(sessionIds, ct);
        var moveTargets = await reader.MoveTargetsAsync(targetIds, ct);

        return DaySessions.Build(localDate, utcNow, sessions, bookingChanges, policy, moveTargets);
    }
}
