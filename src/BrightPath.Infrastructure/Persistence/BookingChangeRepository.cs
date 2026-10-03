using BrightPath.Application.Abstractions;
using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class BookingChangeRepository(BrightPathDbContext db) : IBookingChangeRepository
{
    public Task<List<BookingChange>> ChangesOfAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct) =>
        db.BookingChanges.AsNoTracking().Where(c => sessionIds.Contains(c.SessionId)).ToListAsync(ct);

    public void AddChanges(params BookingChange[] changes) => db.BookingChanges.AddRange(changes);
}
