using BrightPath.Common;

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
    public static List<ScheduleViolation> FindConflicts(
        RuleSession ruleSession, IEnumerable<RuleSession> sameDayRuleSessions, DateTimeOffset now, BookingPolicy policy)
    {
        var conflicts = new List<ScheduleViolation>();
        if (ruleSession.StartsAt < now)
        {
            conflicts.Add(new ScheduleViolation(
                RuleCodes.InThePast,
                DateTimeUtils.GetLocalDate(policy.Zone, ruleSession.StartsAt),
                [ruleSession.Id],
                [],
                $"The session starts at {DateTimeUtils.FormatLocalDateTime(policy.Zone, ruleSession.StartsAt)}, " +
                $"before now ({DateTimeUtils.FormatLocalDateTime(policy.Zone, now)})."));
        }

        // The day as it would be with the booking: an older copy of the candidate is replaced, not counted twice.
        List<RuleSession> dayWithCandidate = [.. sameDayRuleSessions.Where(session => session.Id != ruleSession.Id), ruleSession];

        var candidateViolations = ScheduleRules
            .Check(dayWithCandidate, policy)
            .Where(violation => violation.SessionIds.Contains(ruleSession.Id));

        conflicts.AddRange(candidateViolations);

        return conflicts;
    }
}
