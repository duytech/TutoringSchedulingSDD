using BrightPath.Application.Abstractions;
using BrightPath.Application.Sessions;
using BrightPath.Application.Tutors;
using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class SessionReader(BrightPathDbContext db, BookingPolicy policy) : ISessionReader
{
    public Task<List<GetDaySessionsResponse>> GetDaySessionsAsync(DateOnly date, CancellationToken ct) =>
        db.Sessions.AsNoTracking().StartingOn(date, policy).ToGetDaySessionsResponses().ToListAsync(ct);

    public Task<List<GetTutorDayResponse>> GetTutorDayAsync(DateOnly date, string tutorId, CancellationToken ct) =>
        db.Sessions.AsNoTracking()
            .StartingOn(date, policy)
            .Where(s => s.TutorId == tutorId)
            .ToGetTutorDayResponses()
            .ToListAsync(ct);

    public Task<GetSessionResponse?> GetSessionAsync(Guid id, CancellationToken ct) =>
        db.Sessions.AsNoTracking().Where(s => s.Id == id).ToGetSessionResponses().SingleOrDefaultAsync(ct);

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
