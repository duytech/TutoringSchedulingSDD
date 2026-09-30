using BrightPath.Domain;

namespace BrightPath.Application.Abstractions;

/// <summary>Sessions as the write use cases need them. What it returns is tracked, so changes are saved by <see cref="IUnitOfWork"/>.</summary>
public interface ISessionRepository
{
    /// <summary>
    /// The session, locked until the transaction ends, with its attendees and their students. Null when there
    /// is no such session. Writes to the same session take turns.
    /// </summary>
    Task<Session?> GetForUpdateAsync(Guid id, CancellationToken ct);

    /// <summary>The not-cancelled sessions whose local start date is <paramref name="date"/>.</summary>
    Task<List<RuleSession>> ActiveOnAsync(DateOnly date, CancellationToken ct);

    /// <summary>The not-cancelled sessions whose local start date is between the bounds, both inclusive. A missing bound is open.</summary>
    Task<List<RuleSession>> ActiveBetweenAsync(DateOnly? from, DateOnly? to, CancellationToken ct);

    void Add(Session session);

    void AddChanges(params BookingChange[] changes);
}
