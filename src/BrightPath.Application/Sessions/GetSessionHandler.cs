using BrightPath.Application.Abstractions;
using BrightPath.Application.Common;
using BrightPath.Application.Schedule;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

/// <summary>One session, in the same shape as an item of the schedule. The write use cases answer with it too.</summary>
public sealed class GetSessionHandler(IScheduleReader reader, BookingPolicy policy, TimeProvider clock)
{
    public async Task<Result<ScheduleSessionView>> HandleAsync(Guid id, CancellationToken ct)
    {
        var view = await LoadAsync(id, clock.GetUtcNow(), ct);
        return view is null ? new NotFoundError("Session not found", $"No session {id}.") : view;
    }

    /// <summary>Null when there is no such session.</summary>
    public async Task<ScheduleSessionView?> LoadAsync(Guid id, DateTimeOffset now, CancellationToken ct)
    {
        var session = await reader.DaySessionAsync(id, ct);
        if (session is null)
        {
            return null;
        }

        var changes = await reader.ChangesOfAsync([session], ct);
        var moveTargets = await reader.MoveTargetsAsync([session], ct);
        return ScheduleDay.View(session, changes, now, policy, moveTargets);
    }
}
