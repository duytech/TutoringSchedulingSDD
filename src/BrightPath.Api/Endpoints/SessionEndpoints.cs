using System.Globalization;
using System.Text.RegularExpressions;
using BrightPath.Api.Data;
using BrightPath.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BrightPath.Api.Endpoints;

/// <summary><c>startsAt</c> is a string so a value without an offset can be refused instead of guessed.</summary>
public sealed record CreateSessionRequest(
    string? TutorId, string? RoomId, string? StartsAt, int? DurationMin, Guid[]? StudentIds);

public static partial class SessionEndpoints
{
    private static readonly int[] Durations = [60, 90];

    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var sessions = app.MapGroup("/api/sessions").WithTags("Sessions");

        sessions.MapPost("/", CreateSession)
            .WithName("CreateSession")
            .WithSummary("Book a session, or refuse it with every rule it breaks")
            .WithDescription(
                "startsAt is a local time with its offset (2026-03-07T13:00:00+07:00). durationMin is 60 or 90. " +
                "studentIds are 1 or 2 students (ids from /api/schedule). A 409 lists every conflict at once: " +
                "in-the-past, room-overlap, tutor-overlap, student-overlap, tutor-load, closed-day, outside-hours, " +
                "too-many-attendees.")
            .Produces<ScheduleSessionView>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        sessions.MapGet("/{id:guid}", GetSession)
            .WithName("GetSession")
            .WithSummary("One session, in the same shape as an item of /api/schedule")
            .Produces<ScheduleSessionView>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> CreateSession(
        CreateSessionRequest request, BrightPathDbContext db, BookingPolicy policy, TimeProvider clock,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        var tutor = string.IsNullOrWhiteSpace(request.TutorId)
            ? null
            : await db.Tutors.AsNoTracking().SingleOrDefaultAsync(t => t.Id == request.TutorId, ct);
        if (tutor is null)
        {
            errors["tutorId"] = [$"No tutor '{request.TutorId}'."];
        }

        var roomExists = !string.IsNullOrWhiteSpace(request.RoomId)
            && await db.Rooms.AnyAsync(r => r.Id == request.RoomId, ct);
        if (!roomExists)
        {
            errors["roomId"] = [$"No room '{request.RoomId}'."];
        }

        if (!TryParseWithOffset(request.StartsAt, out var startsAt))
        {
            errors["startsAt"] = ["A local time with its offset is required, e.g. 2026-03-07T13:00:00+07:00."];
        }

        if (request.DurationMin is not { } duration || !Durations.Contains(duration))
        {
            errors["durationMin"] = ["Must be 60 or 90."];
        }

        var studentIds = request.StudentIds ?? [];
        var students = await db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id)).ToListAsync(ct);
        if (studentIds.Length == 0)
        {
            errors["studentIds"] = ["At least one student is required."];
        }
        else if (studentIds.Distinct().Count() != studentIds.Length)
        {
            errors["studentIds"] = ["A student is listed twice."];
        }
        else if (studentIds.Except(students.Select(s => s.Id)).ToList() is { Count: > 0 } unknown)
        {
            errors["studentIds"] = [$"No student with id {string.Join(", ", unknown)}."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var now = clock.GetUtcNow();
        var startsUtc = startsAt.ToUniversalTime();
        var endsUtc = startsUtc.AddMinutes(request.DurationMin!.Value);
        var date = policy.LocalDate(startsUtc);
        var sessionId = Guid.CreateVersion7();
        var byId = students.ToDictionary(s => s.Id);

        var candidate = new RuleSession(
            sessionId, tutor!.Id, tutor.Name, request.RoomId!, startsUtc, endsUtc, Cancelled: false,
            studentIds.Select(id => new RuleAttendee(id, byId[id].Name, AttendeeStatus.Booked, null)).ToList());

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Tutor load is a count, so no constraint can guard it. Bookings for the same tutor and day take turns,
        // and each one reads the day only after the one before has committed.
        var lockKey = BookingLocks.TutorDay(tutor.Id, date);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))", ct);

        var sameDay = await db.Sessions.AsNoTracking()
            .Where(s => s.CancelledAt == null)
            .StartingOn(date, policy)
            .ToRuleSessions()
            .ToListAsync(ct);

        var conflicts = BookingCheck.Conflicts(candidate, sameDay, now, policy);
        if (conflicts.Count > 0)
        {
            return Conflict(conflicts);
        }

        db.Sessions.Add(new Session
        {
            Id = sessionId,
            TutorId = tutor.Id,
            RoomId = request.RoomId!,
            StartsAt = startsUtc,
            EndsAt = endsUtc,
            Attendees = studentIds
                .Select(id => new Attendee
                {
                    Id = Guid.CreateVersion7(),
                    SessionId = sessionId,
                    StudentId = id,
                    Status = AttendeeStatus.Booked,
                })
                .ToList(),
        });
        db.BookingChanges.Add(new BookingChange
        {
            Id = Guid.CreateVersion7(),
            SessionId = sessionId,
            Kind = ChangeKind.Created,
            ChangedAt = now,
            ChangedBy = CancelledBy.Centre,
            AfterCutoff = policy.IsAfterCutoff(now, startsUtc),
        });

        try
        {
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.ExclusionViolation,
        } pg)
        {
            // A race got past the code checks: someone else took the slot between our read and our insert.
            // Disposing the transaction rolls it back.
            return Conflict([RaceConflict(pg.ConstraintName, candidate, date)]);
        }

        var view = await LoadView(db, sessionId, now, policy, ct);
        return TypedResults.Created($"/api/sessions/{sessionId}", view);
    }

    private static async Task<IResult> GetSession(
        Guid id, BrightPathDbContext db, BookingPolicy policy, TimeProvider clock, CancellationToken ct)
    {
        var view = await LoadView(db, id, clock.GetUtcNow(), policy, ct);
        return view is null
            ? TypedResults.Problem(
                title: "Session not found", detail: $"No session {id}.", statusCode: StatusCodes.Status404NotFound)
            : TypedResults.Ok(view);
    }

    private static async Task<ScheduleSessionView?> LoadView(
        BrightPathDbContext db, Guid id, DateTimeOffset now, BookingPolicy policy, CancellationToken ct)
    {
        var session = await db.Sessions.AsNoTracking().Where(s => s.Id == id).ToDaySessions().SingleOrDefaultAsync(ct);
        if (session is null)
        {
            return null;
        }

        var changes = await db.BookingChanges.AsNoTracking().Where(c => c.SessionId == id).ToListAsync(ct);
        return ScheduleDay.View(session, changes, now, policy);
    }

    private static IResult Conflict(IReadOnlyList<ScheduleViolation> conflicts) =>
        TypedResults.Problem(
            title: "The booking breaks centre rules",
            detail: conflicts.Count == 1 ? "1 conflict." : $"{conflicts.Count} conflicts.",
            statusCode: StatusCodes.Status409Conflict,
            extensions: new Dictionary<string, object?> { ["conflicts"] = conflicts });

    private static ScheduleViolation RaceConflict(string? constraint, RuleSession candidate, DateOnly date)
    {
        var (rule, what) = constraint switch
        {
            "ex_sessions_room_slot" => (RuleCodes.RoomOverlap, candidate.RoomId),
            "ex_sessions_tutor_slot" => (RuleCodes.TutorOverlap, candidate.TutorId),
            _ => (RuleCodes.StudentOverlap, "A student"),
        };
        return new ScheduleViolation(
            rule, date, [candidate.Id], [], $"{what} was booked by someone else at the same moment.");
    }

    private static bool TryParseWithOffset(string? value, out DateTimeOffset result)
    {
        result = default;
        return value is not null
            && OffsetSuffix().IsMatch(value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetSuffix();
}
