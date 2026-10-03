using BrightPath.Application.Abstractions;
using BrightPath.Application.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Reports;

public sealed record ViolationReport(IReadOnlyList<ScheduleViolation> Violations);

/// <summary>Every rule the loaded schedule breaks. The imported history is loaded as it is, so this is where its rule breaks show.</summary>
public sealed class GetViolationsHandler(ISessionRepository sessions, BookingPolicy policy)
{
    /// <summary>Both bounds are local dates, optional and inclusive.</summary>
    public async Task<Result<ViolationReport>> HandleAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        if (from > to)
        {
            return new BadRequestError("Invalid date range", $"'from' ({from:yyyy-MM-dd}) is after 'to' ({to:yyyy-MM-dd}).");
        }

        var ruleSessions = await sessions.ActiveBetweenAsync(from, to, ct);
        return new ViolationReport(ScheduleRules.Check(ruleSessions, policy));
    }
}
