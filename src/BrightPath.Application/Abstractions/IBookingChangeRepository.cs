using BrightPath.Domain;

namespace BrightPath.Application.Abstractions;

/// <summary>The booking changes of sessions. Added changes are saved by <see cref="IUnitOfWork"/>.</summary>
public interface IBookingChangeRepository
{
    /// <summary>The booking changes of the sessions with these ids.</summary>
    Task<List<BookingChange>> ChangesOfAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct);

    void AddChanges(params BookingChange[] changes);
}
