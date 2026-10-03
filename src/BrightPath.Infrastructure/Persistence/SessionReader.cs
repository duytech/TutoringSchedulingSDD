using BrightPath.Application.Abstractions;
using BrightPath.Application.Sessions;
using BrightPath.Application.Tutors;
using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class SessionReader(BrightPathDbContext db, BookingPolicy policy) : ISessionReader
{
    public Task<List<GetDaySessionsResponse>> GetDaySessionsAsync(DateOnly date, CancellationToken ct) =>
        db.Sessions.AsNoTracking()
            .StartingOn(date, policy)
            .Select(s => new GetDaySessionsResponse(
                s.Id,
                s.TutorId,
                s.Tutor.Name,
                s.RoomId,
                s.StartsAt,
                s.EndsAt,
                s.CancelledAt,
                s.MovedToSessionId,
                s.LegacyViolation,
                s.Attendees
                    .Select(a => new GetDaySessionsResponse.Attendee(
                        a.Id, a.StudentId, a.Student.Name, a.SourceLessonId, a.Status, a.CancelledAt,
                        a.CancelledBy, a.Chargeable, a.LegacyViolation, a.Note))
                    .ToList()))
            .ToListAsync(ct);

    public Task<List<GetDaySessionsByTutorResponse>> GetDaySessionsByTutorAsync(DateOnly date, string tutorId, CancellationToken ct) =>
        db.Sessions.AsNoTracking()
            .StartingOn(date, policy)
            .Where(s => s.TutorId == tutorId)
            .Select(s => new GetDaySessionsByTutorResponse(
                s.Id,
                s.TutorId,
                s.Tutor.Name,
                s.RoomId,
                s.StartsAt,
                s.EndsAt,
                s.CancelledAt,
                s.MovedToSessionId,
                s.LegacyViolation,
                s.Attendees
                    .Select(a => new GetDaySessionsByTutorResponse.Attendee(
                        a.Id, a.StudentId, a.Student.Name, a.SourceLessonId, a.Status, a.CancelledAt,
                        a.CancelledBy, a.Chargeable, a.LegacyViolation, a.Note))
                    .ToList()))
            .ToListAsync(ct);

    public Task<GetSessionResponse?> GetSessionAsync(Guid id, CancellationToken ct) =>
        db.Sessions.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new GetSessionResponse(
                s.Id,
                s.TutorId,
                s.Tutor.Name,
                s.RoomId,
                s.StartsAt,
                s.EndsAt,
                s.CancelledAt,
                s.MovedToSessionId,
                s.LegacyViolation,
                s.Attendees
                    .Select(a => new GetSessionResponse.Attendee(
                        a.Id, a.StudentId, a.Student.Name, a.SourceLessonId, a.Status, a.CancelledAt,
                        a.CancelledBy, a.Chargeable, a.LegacyViolation, a.Note))
                    .ToList()))
            .SingleOrDefaultAsync(ct);

    public Task<List<BookingChange>> ChangesOfAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct) =>
        db.BookingChanges.AsNoTracking().Where(c => sessionIds.Contains(c.SessionId)).ToListAsync(ct);

    public async Task<Dictionary<Guid, MovedToView>> MoveTargetsAsync(IReadOnlyCollection<Guid> targetIds, CancellationToken ct)
    {
        if (targetIds.Count == 0)
        {
            return [];
        }

        // A target can be on another day, so it is loaded by id rather than taken from the same day's sessions.
        return await db.Sessions
            .Where(s => targetIds.Contains(s.Id))
            .Select(s => new MovedToView(s.Id, s.StartsAt, s.RoomId))
            .ToDictionaryAsync(t => t.Id, ct);
    }
}
