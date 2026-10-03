using BrightPath.Application.Sessions;
using BrightPath.Application.Tutors;
using BrightPath.Domain;

namespace BrightPath.Application.Abstractions;

/// <summary>Sessions read only, in the shape each read use case needs.</summary>
public interface ISessionReader
{
    /// <summary>Every session whose local start date is <paramref name="date"/>, cancelled ones included.</summary>
    Task<List<GetDaySessionsResponse>> GetDaySessionsAsync(DateOnly date, CancellationToken ct);

    /// <summary>The tutor's sessions whose local start date is <paramref name="date"/>, cancelled ones included.</summary>
    Task<List<GetTutorDayResponse>> GetTutorDayAsync(DateOnly date, string tutorId, CancellationToken ct);

    Task<GetSessionResponse?> GetSessionAsync(Guid id, CancellationToken ct);

    /// <summary>The booking changes of the sessions with these ids.</summary>
    Task<List<BookingChange>> ChangesOfAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct);

    /// <summary>The sessions that moved sessions went to, by id. A target can be on another day.</summary>
    Task<Dictionary<Guid, MovedToView>> MoveTargetsAsync(IReadOnlyCollection<Guid> targetIds, CancellationToken ct);
}
