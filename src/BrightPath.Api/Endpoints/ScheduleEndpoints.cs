using BrightPath.Api.Data;
using BrightPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api.Endpoints;

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var schedule = app.MapGroup("/api/schedule").WithTags("Schedule");

        schedule.MapGet("/", GetSchedule)
            .WithName("GetSchedule")
            .WithSummary("One day's schedule, grouped by room and by tutor")
            .WithDescription(
                "Every session whose local start date is the given date (default: today on the pinned clock), " +
                "cancelled ones included, with attendees, changes and a state relative to now. " +
                "rooms and tutors list every room and tutor with the IDs of their sessions that day.")
            .Produces<ScheduleDayView>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetSchedule(
        DateOnly? date, BrightPathDbContext db, BookingPolicy policy, TimeProvider clock, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var day = date ?? policy.LocalDate(now);
        var start = policy.ToInstant(day, TimeOnly.MinValue);
        var end = policy.ToInstant(day.AddDays(1), TimeOnly.MinValue);

        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => s.StartsAt >= start && s.StartsAt < end)
            .Select(s => new DaySession(
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
                    .Select(a => new DayAttendee(
                        a.Id, a.StudentId, a.Student.Name, a.SourceLessonId, a.Status, a.CancelledAt,
                        a.CancelledBy, a.Chargeable, a.LegacyViolation, a.Note))
                    .ToList()))
            .ToListAsync(ct);

        // BookingChange has no navigation from Session, so its rows come in a query of their own.
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var changes = await db.BookingChanges.AsNoTracking()
            .Where(c => sessionIds.Contains(c.SessionId))
            .ToListAsync(ct);

        var roomIds = await db.Rooms.AsNoTracking().Select(r => r.Id).ToListAsync(ct);
        var tutors = await db.Tutors.AsNoTracking().ToListAsync(ct);

        return TypedResults.Ok(ScheduleDay.Build(day, now, sessions, changes, roomIds, tutors, policy));
    }
}
