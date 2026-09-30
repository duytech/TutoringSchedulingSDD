using System.Globalization;

namespace BrightPath.Domain;

/// <summary>
/// The conflicts of a new session: it must not start in the past, and it must break none of the centre rules
/// together with the sessions already on its day. Pure: no I/O, no database.
/// </summary>
public static class BookingCheck
{
    /// <summary>
    /// Only violations the new session is part of. A rule the day already broke (T1's 7 sessions on 03-06) is not
    /// the new booking's fault, unless the booking adds to it. <c>in-the-past</c> comes first.
    /// </summary>
    public static List<ScheduleViolation> Conflicts(
        RuleSession candidate, IEnumerable<RuleSession> sameDay, DateTimeOffset now, BookingPolicy policy)
    {
        var conflicts = new List<ScheduleViolation>();
        if (candidate.StartsAt < now)
        {
            conflicts.Add(new ScheduleViolation(
                RuleCodes.InThePast,
                policy.LocalDate(candidate.StartsAt),
                [candidate.Id],
                [],
                $"The session starts at {Format(policy.ToLocal(candidate.StartsAt))}, " +
                $"before now ({Format(policy.ToLocal(now))})."));
        }

        conflicts.AddRange(ScheduleRules
            .Check([.. sameDay.Where(s => s.Id != candidate.Id), candidate], policy)
            .Where(v => v.SessionIds.Contains(candidate.Id)));

        return conflicts;
    }

    private static string Format(DateTimeOffset local) => local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
