using BrightPath.Application.Students;

namespace BrightPath.Api.Endpoints;

public static class StudentEndpoints
{
    public static IEndpointRouteBuilder MapStudentEndpoints(this IEndpointRouteBuilder app)
    {
        var students = app.MapGroup("/api/students").WithTags("Students");

        students.MapGet("/", GetStudents)
            .WithName("GetStudents")
            .WithSummary("Every student")
            .WithDescription(
                "Every student with their id and name, ordered by name. The ids are the studentIds that " +
                "POST /api/sessions takes. Reference data: the same on every day.")
            .Produces<IReadOnlyList<StudentView>>();

        return app;
    }

    private static async Task<IResult> GetStudents(GetStudentsHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(ct));
}
