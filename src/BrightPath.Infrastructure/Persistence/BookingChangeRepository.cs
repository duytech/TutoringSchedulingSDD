using BrightPath.Application.Abstractions;
using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class BookingChangeRepository(BrightPathDbContext dbContext) : IBookingChangeRepository
{
    public Task<List<BookingChange>> ListChangesAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct) =>
        dbContext.BookingChanges.AsNoTracking().Where(c => sessionIds.Contains(c.SessionId)).ToListAsync(ct);

    public void AddChanges(params BookingChange[] changes) => dbContext.BookingChanges.AddRange(changes);
}
