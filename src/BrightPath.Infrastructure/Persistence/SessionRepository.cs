using BrightPath.Application.Abstractions;
using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class SessionRepository(BrightPathDbContext dbContext, BookingPolicy bookingPolicy) : ISessionRepository
{
    public async Task<Session?> GetForUpdateAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await dbContext.Sessions
            .FromSql($"SELECT * FROM sessions WHERE id = {sessionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct);
        if (session is null)
        {
            return null;
        }

        // Loading the attendees into the same context fills session.Attendees.
        await dbContext.Attendees.Include(a => a.Student).Where(a => a.SessionId == sessionId).LoadAsync(ct);
        return session;
    }

    public Task<List<RuleSession>> ListActiveOnAsync(DateOnly date, CancellationToken ct) =>
        ListActiveBetweenAsync(date, date, ct);

    public Task<List<RuleSession>> ListActiveBetweenAsync(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        dbContext.Sessions.AsNoTracking()
            .Where(s => s.CancelledAt == null)
            .FilterStartingBetween(from, to, bookingPolicy)
            .Select(s => new RuleSession(
                s.Id,
                s.TutorId,
                s.Tutor.Name,
                s.RoomId,
                s.StartsAt,
                s.EndsAt,
                s.CancelledAt != null,
                s.Attendees
                    .Select(a => new RuleAttendee(a.StudentId, a.Student.Name, a.Status, a.SourceLessonId))
                    .ToList()))
            .ToListAsync(ct);

    public void Add(Session session) => dbContext.Sessions.Add(session);
}
