using BrightPath.Application.Abstractions;
using BrightPath.Application.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

public sealed record CancelAttendeeRequest(string? CancelledBy, string? Note);

/// <summary>Cancel one student's place, freeing the slot. The last one out cancels the session too.</summary>
public sealed class CancelAttendeeHandler(
    ISessionRepository sessionRepository,
    IBookingChangeRepository bookingChangeRepository,
    IUnitOfWork unitOfWork,
    SessionViewLoader sessionViewLoader,
    BookingPolicy bookingPolicy,
    TimeProvider timeProvider)
{
    public async Task<Result<SessionView>> HandleAsync(
        Guid sessionId, Guid attendeeId, CancelAttendeeRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.CancelledBy is not { } cancelledBy || !CancelledBy.All.Contains(cancelledBy))
        {
            errors["cancelledBy"] = [$"Must be one of: {string.Join(", ", CancelledBy.All)}."];
        }

        if (request.Note is { Length: > SessionInput.MaxNoteLength })
        {
            errors["note"] = [SessionInput.NoteError];
        }

        if (errors.Count > 0)
        {
            return new ValidationError(errors);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        // Two cancels on one session take turns, so the last one out always sees the other one gone
        // and cancels the session.
        var session = await sessionRepository.GetForUpdateAsync(sessionId, ct);
        var attendee = session?.Attendees.SingleOrDefault(a => a.Id == attendeeId);
        if (session is null || attendee is null)
        {
            return new NotFoundError("Attendee not found", $"Session {sessionId} has no attendee {attendeeId}.");
        }

        var now = timeProvider.GetUtcNow();
        var decision = CancelCheck.Decide(
            new CancelSession(
                session.Id,
                session.StartsAt,
                session.Attendees
                    .Select(a => new CancelAttendee(
                        a.Id, a.Student.Name, a.Status, a.CancelledAt, a.CancelledBy, a.SourceLessonId))
                    .ToList()),
            attendeeId,
            request.CancelledBy!,
            now,
            bookingPolicy);
        if (decision.Conflicts.Count > 0)
        {
            return new ConflictError(decision.Conflicts);
        }

        attendee.Status = AttendeeStatus.Cancelled;
        attendee.CancelledAt = now;
        attendee.CancelledBy = request.CancelledBy;
        attendee.Chargeable = decision.Chargeable;
        bookingChangeRepository.AddChanges(new BookingChange
        {
            Id = Guid.CreateVersion7(),
            SessionId = sessionId,
            AttendeeId = attendeeId,
            Kind = ChangeKind.Cancelled,
            ChangedAt = now,
            ChangedBy = request.CancelledBy,
            AfterCutoff = decision.AfterCutoff,
            Note = request.Note,
        });

        if (decision.CancelsSession)
        {
            // The last one out cancels the session, with a change of its own so the tutor sees the slot is gone.
            session.CancelledAt = now;
            bookingChangeRepository.AddChanges(new BookingChange
            {
                Id = Guid.CreateVersion7(),
                SessionId = sessionId,
                Kind = ChangeKind.Cancelled,
                ChangedAt = now,
                ChangedBy = request.CancelledBy,
                AfterCutoff = decision.AfterCutoff,
            });
        }

        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return (await sessionViewLoader.LoadAsync(sessionId, ct))!;
    }
}
