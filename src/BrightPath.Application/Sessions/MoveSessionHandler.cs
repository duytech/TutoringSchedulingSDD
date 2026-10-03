using BrightPath.Application.Abstractions;
using BrightPath.Application.Common;
using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

/// <summary><c>roomId</c> and <c>durationMin</c> default to the old session's. The tutor never changes.</summary>
public sealed record MoveSessionRequest(
    string? StartsAt, string? RoomId, int? DurationMin, string? MovedBy, string? Note);

/// <summary>
/// Move a session to a new time, room or length. In one transaction the old session is cancelled, the new one is
/// created, and the old one points at the new one.
/// </summary>
public sealed class MoveSessionHandler(
    IReferenceData referenceData,
    ISessionRepository sessions,
    IBookingLocks locks,
    IUnitOfWork unitOfWork,
    GetSessionHandler views,
    BookingPolicy policy,
    TimeProvider clock)
{
    public async Task<Result<SessionView>> HandleAsync(Guid id, MoveSessionRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();
        if (!SessionInput.TryParseWithOffset(request.StartsAt, out var startsAt))
        {
            errors["startsAt"] = [SessionInput.StartsAtError];
        }

        if (request.RoomId is { } roomId && !await referenceData.RoomExistsAsync(roomId, ct))
        {
            errors["roomId"] = [$"No room '{roomId}'."];
        }

        if (request.DurationMin is { } duration && !SessionInput.Durations.Contains(duration))
        {
            errors["durationMin"] = [SessionInput.DurationError];
        }

        if (request.MovedBy is not { } movedBy || !CancelledBy.All.Contains(movedBy))
        {
            errors["movedBy"] = [$"Must be one of: {string.Join(", ", CancelledBy.All)}."];
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

        // A move and a cancel of the same session take turns, so a session cannot be moved after it was cancelled.
        var old = await sessions.GetForUpdateAsync(id, ct);
        if (old is null)
        {
            return new NotFoundError("Session not found", $"No session {id}.");
        }

        var source = new MoveSource(
            old.Id, old.RoomId, old.StartsAt, old.EndsAt, old.CancelledAt,
            old.Attendees.Select(a => a.SourceLessonId).OfType<string>().Order(StringComparer.Ordinal).ToList());

        var startsUtc = startsAt.ToUniversalTime();
        var newRoomId = request.RoomId ?? old.RoomId;
        var newDuration = request.DurationMin ?? (int)(old.EndsAt - old.StartsAt).TotalMinutes;
        if (MoveCheck.IsNoop(source, startsUtc, newRoomId, newDuration))
        {
            return new ValidationError(new Dictionary<string, string[]>
            {
                ["startsAt"] = ["Nothing to move: same time, room and length."],
            });
        }

        var now = clock.GetUtcNow();
        var date = DateTimeUtils.LocalDate(policy.Zone, startsUtc);
        var tutor = await referenceData.FindTutorAsync(old.TutorId, ct);
        var booked = old.Attendees.Where(a => a.Status == AttendeeStatus.Booked).ToList();
        var newId = Guid.CreateVersion7();
        var candidate = new RuleSession(
            newId, old.TutorId, tutor!.Name, newRoomId, startsUtc, startsUtc.AddMinutes(newDuration), Cancelled: false,
            booked.Select(a => new RuleAttendee(a.StudentId, a.Student.Name, AttendeeStatus.Booked, null)).ToList());

        // Same lock as create: the new day's tutor load is counted with no other booking for that tutor in between.
        await locks.TutorDayAsync(old.TutorId, date, ct);

        var sameDay = await sessions.ActiveOnAsync(date, ct);

        var conflicts = MoveCheck.Conflicts(source, candidate, sameDay, now, policy);
        if (conflicts.Count > 0)
        {
            return new ConflictError(conflicts);
        }

        // Give the old slot up first, so a move that overlaps it (14:00 -> 14:30 in the same room) does not
        // collide with its own old row in the constraints. A move is never chargeable (Q8).
        old.CancelledAt = now;
        foreach (var attendee in booked)
        {
            attendee.Status = AttendeeStatus.Cancelled;
            attendee.CancelledAt = now;
            attendee.CancelledBy = request.MovedBy;
            attendee.Chargeable = false;
        }

        await unitOfWork.SaveChangesAsync(ct);

        var (noteOnOld, noteOnNew) = MoveCheck.Notes(source, startsUtc, newRoomId, request.Note, policy);
        sessions.Add(new Session
        {
            Id = newId,
            TutorId = old.TutorId,
            RoomId = newRoomId,
            StartsAt = candidate.StartsAt,
            EndsAt = candidate.EndsAt,
            Attendees = booked
                .Select(a => new Attendee
                {
                    Id = Guid.CreateVersion7(),
                    SessionId = newId,
                    StudentId = a.StudentId,
                    Status = AttendeeStatus.Booked,
                })
                .ToList(),
        });
        old.MovedToSessionId = newId;
        sessions.AddChanges(
            new BookingChange
            {
                Id = Guid.CreateVersion7(),
                SessionId = old.Id,
                Kind = ChangeKind.Moved,
                ChangedAt = now,
                ChangedBy = request.MovedBy,
                AfterCutoff = policy.IsAfterCutoff(now, old.StartsAt),
                Note = noteOnOld,
            },
            new BookingChange
            {
                Id = Guid.CreateVersion7(),
                SessionId = newId,
                Kind = ChangeKind.Moved,
                ChangedAt = now,
                ChangedBy = request.MovedBy,
                AfterCutoff = policy.IsAfterCutoff(now, startsUtc),
                Note = noteOnNew,
            });

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (SlotTakenException ex)
        {
            // Disposing the transaction rolls back the cancel too.
            return new ConflictError([RaceConflict.For(ex.Slot, candidate, date)]);
        }

        return (await views.LoadAsync(newId, now, ct))!;
    }
}
