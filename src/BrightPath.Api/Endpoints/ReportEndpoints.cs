using BrightPath.Api.Data;
using BrightPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api.Endpoints;

public sealed record ViolationReport(IReadOnlyList<ScheduleViolation> Violations);

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
        DateOnly? from, DateOnly? to, BrightPathDbContext db, BookingPolicy policy, CancellationToken ct)
    {
        if (from > to)
        {
            return TypedResults.Problem(
                title: "Invalid date range",
                detail: $"'from' ({from:yyyy-MM-dd}) is after 'to' ({to:yyyy-MM-dd}).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var ruleSessions = await db.Sessions.AsNoTracking()
            .Where(s => s.CancelledAt == null)
            .StartingBetween(from, to, policy)
            .ToRuleSessions()
            .ToListAsync(ct);

        return TypedResults.Ok(new ViolationReport(ScheduleRules.Check(ruleSessions, policy)));
    }
}
