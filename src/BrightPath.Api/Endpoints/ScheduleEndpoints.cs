using BrightPath.Application.Schedule;

namespace BrightPath.Api.Endpoints;

public static class ScheduleEndpoints
{
    public static IEndpointRouteBuilder MapScheduleEndpoints(this IEndpointRouteBuilder app)
    {
        var schedule = app.MapGroup("/api/schedule").WithTags("Schedule");

        schedule.MapGet("/", GetSchedule)
            .WithName("GetSchedule")
            .WithSummary("One day's schedule")
            .WithDescription(
                "Every session whose local start date is the given date (default: today on the pinned clock), " +
                "cancelled ones included, with attendees, changes and a state relative to now. " +
                "The rooms and tutors come from /api/rooms and /api/tutors.")
            .Produces<ScheduleDayView>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetSchedule(DateOnly? date, GetScheduleHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(date, ct));
}
