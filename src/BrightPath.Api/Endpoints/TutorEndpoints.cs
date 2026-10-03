using BrightPath.Api.Http;
using BrightPath.Application.Tutors;

namespace BrightPath.Api.Endpoints;

public static class TutorEndpoints
{
    public static IEndpointRouteBuilder MapTutorEndpoints(this IEndpointRouteBuilder app)
    {
        var tutors = app.MapGroup("/api/tutors").WithTags("Tutors");

        tutors.MapGet("/", GetTutors)
            .WithName("GetTutors")
            .WithSummary("Every tutor")
            .WithDescription("Every tutor with their name and subject, ordered by id. Reference data: the same on every day.")
            .Produces<IReadOnlyList<TutorView>>();

        tutors.MapGet("/{id}/day", GetTutorDay)
            .WithName("GetTutorDay")
            .WithSummary("One tutor's day, with its cut-off")
            .WithDescription(
                "Every session of the tutor whose local start date is the given date (default: today on the pinned " +
                "clock), cancelled and moved ones included, in the same shape as GET /api/sessions. cutoff is 16:00 the " +
                "day before, final is true once it has passed, and each change has afterCutoff set when it was made " +
                "after it. A tutor with nothing that day gets an empty list.")
            .Produces<TutorDaySheetView>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    private static async Task<IResult> GetTutors(GetTutorsHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(ct));

    private static async Task<IResult> GetTutorDay(
        string id, DateOnly? date, GetTutorDayHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(id, date, ct)).ToHttp(sheet => TypedResults.Ok(sheet));
}
