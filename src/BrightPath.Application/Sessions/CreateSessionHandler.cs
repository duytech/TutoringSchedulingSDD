using BrightPath.Application.Abstractions;
using BrightPath.Application.Common;
using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

/// <summary><c>startsAt</c> is a string so a value without an offset can be refused instead of guessed.</summary>
public sealed record CreateSessionRequest(
    string? TutorId, string? RoomId, string? StartsAt, int? DurationMin, Guid[]? StudentIds);

/// <summary>Book a session, or refuse it with every rule it breaks.</summary>
public sealed class CreateSessionHandler(
    ITutorRepository tutorRepository,
    IRoomRepository roomRepository,
    IStudentRepository studentRepository,
    ISessionRepository sessionRepository,
    IBookingChangeRepository bookingChangeRepository,
    IBookingLocks bookingLocks,
    IUnitOfWork unitOfWork,
    SessionViewLoader sessionViewLoader,
    BookingPolicy bookingPolicy,
    TimeProvider timeProvider)
{
    public async Task<Result<SessionView>> HandleAsync(CreateSessionRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        var tutor = string.IsNullOrWhiteSpace(request.TutorId)
            ? null
            : await tutorRepository.FindAsync(request.TutorId, ct);
        if (tutor is null)
        {
            errors["tutorId"] = [$"No tutor '{request.TutorId}'."];
        }

        var roomExists = !string.IsNullOrWhiteSpace(request.RoomId)
            && await roomRepository.ExistsAsync(request.RoomId, ct);
        if (!roomExists)
        {
            errors["roomId"] = [$"No room '{request.RoomId}'."];
        }

        if (!SessionInput.TryParseWithOffset(request.StartsAt, out var startsAt))
        {
            errors["startsAt"] = [SessionInput.StartsAtError];
        }

        if (request.DurationMin is not { } duration || !SessionInput.Durations.Contains(duration))
        {
            errors["durationMin"] = [SessionInput.DurationError];
        }

        var studentIds = request.StudentIds ?? [];
        var foundStudents = await studentRepository.FindManyAsync(studentIds, ct);
        if (studentIds.Length == 0)
        {
            errors["studentIds"] = ["At least one student is required."];
        }
        else if (studentIds.Distinct().Count() != studentIds.Length)
        {
            errors["studentIds"] = ["A student is listed twice."];
        }
        else if (studentIds.Except(foundStudents.Select(s => s.Id)).ToList() is { Count: > 0 } unknown)
        {
            errors["studentIds"] = [$"No student with id {string.Join(", ", unknown)}."];
        }

        if (errors.Count > 0)
        {
            return new ValidationError(errors);
        }

        var now = timeProvider.GetUtcNow();
        var startsUtc = startsAt.ToUniversalTime();
        var endsUtc = startsUtc.AddMinutes(request.DurationMin!.Value);
        var date = DateTimeUtils.GetLocalDate(bookingPolicy.Zone, startsUtc);
        var sessionId = Guid.CreateVersion7();
        var byId = foundStudents.ToDictionary(s => s.Id);

        var candidate = new RuleSession(
            sessionId, tutor!.Id, tutor.Name, request.RoomId!, startsUtc, endsUtc, Cancelled: false,
            studentIds.Select(studentId => new RuleAttendee(studentId, byId[studentId].Name, AttendeeStatus.Booked, null)).ToList());

        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);

        // Bookings for the same tutor and day take turns, so the tutor load is counted with no other booking in between.
        await bookingLocks.LockTutorDayAsync(tutor.Id, date, ct);

        var sameDay = await sessionRepository.ListActiveOnAsync(date, ct);

        var conflicts = BookingCheck.FindConflicts(candidate, sameDay, now, bookingPolicy);
        if (conflicts.Count > 0)
        {
            return new ConflictError(conflicts);
        }

        sessionRepository.Add(new Session
        {
            Id = sessionId,
            TutorId = tutor.Id,
            RoomId = request.RoomId!,
            StartsAt = startsUtc,
            EndsAt = endsUtc,
            Attendees = studentIds
                .Select(studentId => new Attendee
                {
                    Id = Guid.CreateVersion7(),
                    SessionId = sessionId,
                    StudentId = studentId,
                    Status = AttendeeStatus.Booked,
                })
                .ToList(),
        });
        bookingChangeRepository.AddChanges(new BookingChange
        {
            Id = Guid.CreateVersion7(),
            SessionId = sessionId,
            Kind = ChangeKind.Created,
            ChangedAt = now,
            ChangedBy = CancelledBy.Centre,
            AfterCutoff = bookingPolicy.IsAfterCutoff(now, startsUtc),
        });

        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (SlotTakenException ex)
        {
            // Disposing the transaction rolls it back.
            return new ConflictError([RaceConflict.Create(ex.Slot, candidate, date)]);
        }

        return (await sessionViewLoader.LoadAsync(sessionId, ct))!;
    }
}
