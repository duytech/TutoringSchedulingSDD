using BrightPath.Api.Data;
using BrightPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api.Endpoints;

public static class TutorEndpoints
{
    public static IEndpointRouteBuilder MapTutorEndpoints(this IEndpointRouteBuilder app)
    {
        var tutors = app.MapGroup("/api/tutors").WithTags("Tutors");

        tutors.MapGet("/{id}/day", GetTutorDay)
            .WithName("GetTutorDay")
            .WithSummary("One tutor's day, with what changed after they were told")
            .WithDescription(
                "Every session of the tutor whose local start date is the given date (default: today on the pinned " +
                "clock), cancelled and moved ones included, in the same shape as /api/schedule. cutoff is 16:00 the " +
                "day before, final is true once it has passed, and changesAfterCutoff lists every change made after " +
                "it, oldest first. A tutor with nothing that day gets empty lists.")
            .Produces<TutorDaySheetView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetTutorDay(
        string id, DateOnly? date, BrightPathDbContext db, BookingPolicy policy, TimeProvider clock,
        CancellationToken ct)
    {
        var tutor = await db.Tutors.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);
        if (tutor is null)
        {
            return TypedResults.Problem(
                title: "Tutor not found", detail: $"No tutor {id}.", statusCode: StatusCodes.Status404NotFound);
        }

        var now = clock.GetUtcNow();
        var day = date ?? policy.LocalDate(now);
        var sessions = await db.Sessions.AsNoTracking()
            .StartingOn(day, policy)
            .Where(s => s.TutorId == id)
            .ToDaySessions()
            .ToListAsync(ct);
        var changes = await db.BookingChanges.AsNoTracking().ChangesOfAsync(sessions, ct);
        var moveTargets = await db.Sessions.AsNoTracking().MoveTargetsAsync(sessions, ct);

        return TypedResults.Ok(TutorDaySheet.Build(tutor, day, now, sessions, changes, policy, moveTargets));
    }
}
