using BrightPath.Application.Schedule;
using BrightPath.Domain;

namespace BrightPath.Application.Abstractions;

/// <summary>Sessions as the day views show them, read only.</summary>
public interface IScheduleReader
{
    /// <summary>Every session whose local start date is <paramref name="date"/>, cancelled ones included. Only one tutor's when <paramref name="tutorId"/> is set.</summary>
    Task<List<DaySession>> DaySessionsAsync(DateOnly date, string? tutorId, CancellationToken ct);

    Task<DaySession?> DaySessionAsync(Guid id, CancellationToken ct);

    /// <summary>The booking changes of the sessions with these ids.</summary>
    Task<List<BookingChange>> ChangesOfAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct);

    /// <summary>The sessions that moved sessions went to, by id. A target can be on another day.</summary>
    Task<Dictionary<Guid, MovedToView>> MoveTargetsAsync(IReadOnlyCollection<Guid> targetIds, CancellationToken ct);
}
