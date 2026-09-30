using BrightPath.Api.Http;
using BrightPath.Application.Reports;

namespace BrightPath.Api.Endpoints;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder app)
    {
        var reports = app.MapGroup("/api/reports").WithTags("Reports");

        reports.MapGet("/violations", GetViolations)
            .WithName("GetViolations")
            .WithSummary("Every rule the loaded schedule breaks")
            .WithDescription(
                "Runs the centre rules over the sessions whose local start date is between from and to " +
                "(both optional, both inclusive). The imported history is loaded as it is, so this is where its rule breaks show.")
            .Produces<ViolationReport>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> GetViolations(
        DateOnly? from, DateOnly? to, GetViolationsHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(from, to, ct)).ToHttp(report => TypedResults.Ok(report));
}
