using BrightPath.Application.Abstractions;
using BrightPath.Application.Schedule;
using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class ScheduleReader(BrightPathDbContext db, BookingPolicy policy) : IScheduleReader
{
    public Task<List<DaySession>> DaySessionsAsync(DateOnly date, string? tutorId, CancellationToken ct)
    {
        var sessions = db.Sessions.AsNoTracking().StartingOn(date, policy);
        if (tutorId is not null)
        {
            sessions = sessions.Where(s => s.TutorId == tutorId);
        }

        return sessions.ToDaySessions().ToListAsync(ct);
    }

    public Task<DaySession?> DaySessionAsync(Guid id, CancellationToken ct) =>
        db.Sessions.AsNoTracking().Where(s => s.Id == id).ToDaySessions().SingleOrDefaultAsync(ct);

    public Task<List<BookingChange>> ChangesOfAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct) =>
        db.BookingChanges.AsNoTracking().ChangesOf(sessionIds).ToListAsync(ct);

    public async Task<Dictionary<Guid, MovedToView>> MoveTargetsAsync(IReadOnlyCollection<Guid> targetIds, CancellationToken ct)
    {
        if (targetIds.Count == 0)
        {
            return [];
        }

        return await db.Sessions.MoveTargets(targetIds).ToDictionaryAsync(t => t.Id, ct);
    }
}
