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

        var sessions = db.Sessions.AsNoTracking().Where(s => s.CancelledAt == null);
        if (from is { } f)
        {
            var start = policy.ToInstant(f, TimeOnly.MinValue);
            sessions = sessions.Where(s => s.StartsAt >= start);
        }
        if (to is { } t)
        {
            var end = policy.ToInstant(t.AddDays(1), TimeOnly.MinValue);
            sessions = sessions.Where(s => s.StartsAt < end);
        }

        var ruleSessions = await sessions
            .Select(s => new RuleSession(
                s.Id,
                s.TutorId,
                s.Tutor.Name,
                s.RoomId,
                s.StartsAt,
                s.EndsAt,
                s.CancelledAt != null,
                s.Attendees
                    .Select(a => new RuleAttendee(a.StudentId, a.Student.Name, a.Status, a.SourceLessonId))
                    .ToList()))
            .ToListAsync(ct);

        return TypedResults.Ok(new ViolationReport(ScheduleRules.Check(ruleSessions, policy)));
    }
}
