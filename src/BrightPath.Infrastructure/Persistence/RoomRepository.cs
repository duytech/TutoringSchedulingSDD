using BrightPath.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class RoomRepository(BrightPathDbContext db) : IRoomRepository
{
    public Task<bool> ExistsAsync(string id, CancellationToken ct) =>
        db.Rooms.AnyAsync(r => r.Id == id, ct);

    public Task<List<string>> ListIdsAsync(CancellationToken ct) =>
        db.Rooms.AsNoTracking().Select(r => r.Id).ToListAsync(ct);
}
