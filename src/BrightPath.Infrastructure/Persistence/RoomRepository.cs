using BrightPath.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class RoomRepository(BrightPathDbContext dbContext) : IRoomRepository
{
    public Task<bool> ExistsAsync(string roomId, CancellationToken ct) =>
        dbContext.Rooms.AnyAsync(r => r.Id == roomId, ct);

    public Task<List<string>> ListIdsAsync(CancellationToken ct) =>
        dbContext.Rooms.AsNoTracking().Select(r => r.Id).ToListAsync(ct);
}
