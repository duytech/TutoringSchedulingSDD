using BrightPath.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

/// <summary>Postgres advisory locks, released when the transaction ends.</summary>
internal sealed class PostgresBookingLocks(BrightPathDbContext dbContext) : IBookingLocks
{
    public async Task LockTutorDayAsync(string tutorId, DateOnly date, CancellationToken ct)
    {
        var lockKey = BookingLocks.BuildTutorDayLockKey(tutorId, date);
        await dbContext.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);
    }
}
