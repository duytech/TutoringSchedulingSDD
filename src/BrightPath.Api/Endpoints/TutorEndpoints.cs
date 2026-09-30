using BrightPath.Api.Http;
using BrightPath.Application.Tutors;

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
        string id, DateOnly? date, GetTutorDayHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(id, date, ct)).ToHttp(sheet => TypedResults.Ok(sheet));
}
