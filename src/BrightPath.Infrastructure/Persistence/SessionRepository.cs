using BrightPath.Application.Abstractions;
using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class SessionRepository(BrightPathDbContext db, BookingPolicy policy) : ISessionRepository
{
    public async Task<Session?> GetForUpdateAsync(Guid id, CancellationToken ct)
    {
        var session = await db.Sessions
            .FromSql($"SELECT * FROM sessions WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (session is null)
        {
            return null;
        }

        // Loading the attendees into the same context fills session.Attendees.
        await db.Attendees.Include(a => a.Student).Where(a => a.SessionId == id).LoadAsync(ct);
        return session;
    }

    public Task<List<RuleSession>> ActiveOnAsync(DateOnly date, CancellationToken ct) =>
        ActiveBetweenAsync(date, date, ct);

    public Task<List<RuleSession>> ActiveBetweenAsync(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        db.Sessions.AsNoTracking()
            .Where(s => s.CancelledAt == null)
            .StartingBetween(from, to, policy)
            .ToRuleSessions()
            .ToListAsync(ct);

    public void Add(Session session) => db.Sessions.Add(session);

    public void AddChanges(params BookingChange[] changes) => db.BookingChanges.AddRange(changes);
}
