using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Infrastructure.Persistence;

/// <summary>
/// Which sessions start on a local date or date range, written once so the report, the day's sessions and the
/// create check pick the same days.
/// </summary>
public static class SessionQueries
{
    /// <summary>Sessions whose local start date is between from and to, both inclusive. A missing bound is open.</summary>
    public static IQueryable<Session> StartingBetween(
        this IQueryable<Session> sessions, DateOnly? from, DateOnly? to, BookingPolicy policy)
    {
        if (from is { } f)
        {
            var start = DateTimeUtils.LocalToUtc(policy.Zone, f, TimeOnly.MinValue);
            sessions = sessions.Where(s => s.StartsAt >= start);
        }
        if (to is { } t)
        {
            var end = DateTimeUtils.LocalToUtc(policy.Zone, t.AddDays(1), TimeOnly.MinValue);
            sessions = sessions.Where(s => s.StartsAt < end);
        }
        return sessions;
    }

    public static IQueryable<Session> StartingOn(this IQueryable<Session> sessions, DateOnly date, BookingPolicy policy) =>
        sessions.StartingBetween(date, date, policy);
}
