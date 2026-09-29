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
        var sessions = await db.Sessions.AsNoTracking().StartingOn(day, policy).ToDaySessions().ToListAsync(ct);

        // BookingChange has no navigation from Session, so its rows come in a query of their own.
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var changes = await db.BookingChanges.AsNoTracking()
            .Where(c => sessionIds.Contains(c.SessionId))
            .ToListAsync(ct);

        var roomIds = await db.Rooms.AsNoTracking().Select(r => r.Id).ToListAsync(ct);
        var tutors = await db.Tutors.AsNoTracking().ToListAsync(ct);
        var moveTargets = await db.Sessions.AsNoTracking().MoveTargetsAsync(sessions, ct);

        return TypedResults.Ok(ScheduleDay.Build(day, now, sessions, changes, roomIds, tutors, policy, moveTargets));
    }
}
