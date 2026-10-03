using System.Globalization;

namespace BrightPath.Domain;

/// <summary>What the move check needs to know about the session being moved.</summary>
public sealed record MoveSource(
    Guid Id, string RoomId, DateTimeOffset StartsAt, DateTimeOffset EndsAt, DateTimeOffset? CancelledAt,
    IReadOnlyList<string> LessonIds);

/// <summary>
/// Moving a session: the old one must still be ahead and active, and the new slot must pass every rule a new
/// booking passes, with the old session left out of its day, since it is being given up. Pure: no I/O, no database.
/// </summary>
public static class MoveCheck
{
    /// <summary>
    /// The old session's conflicts (<c>already-started</c>, <c>already-cancelled</c>), then the new slot's, in the
    /// create order. <paramref name="sameDay"/> is the new date's active sessions; the old one is removed here.
    /// </summary>
    public static List<ScheduleViolation> Conflicts(
        MoveSource old, RuleSession candidate, IEnumerable<RuleSession> sameDay, DateTimeOffset now, BookingPolicy policy)
    {
        var conflicts = new List<ScheduleViolation>();
        if (now >= old.StartsAt)
        {
            conflicts.Add(OldConflict(
                old,
                RuleCodes.AlreadyStarted,
                $"The session started at {Format(DateTimeUtils.ToLocal(policy.Zone, old.StartsAt))}, before now ({Format(DateTimeUtils.ToLocal(policy.Zone, now))}).",
                policy));
        }

        if (old.CancelledAt is { } cancelledAt)
        {
            conflicts.Add(OldConflict(
                old,
                RuleCodes.AlreadyCancelled,
                $"The session was already cancelled at {Format(DateTimeUtils.ToLocal(policy.Zone, cancelledAt))}.",
                policy));
        }

        conflicts.AddRange(BookingCheck.Conflicts(candidate, sameDay.Where(s => s.Id != old.Id), now, policy));
        return conflicts;
    }

    /// <summary>True when the move would change nothing: the same start, room and length.</summary>
    public static bool IsNoop(MoveSource old, DateTimeOffset startsAt, string roomId, int durationMin) =>
        startsAt == old.StartsAt
        && roomId == old.RoomId
        && durationMin == (int)(old.EndsAt - old.StartsAt).TotalMinutes;

    /// <summary>The notes on the two <c>moved</c> changes: "to …" on the old session, "from …" on the new one.</summary>
    public static (string OnOld, string OnNew) Notes(
        MoveSource old, DateTimeOffset newStartsAt, string newRoomId, string? note, BookingPolicy policy)
    {
        var suffix = string.IsNullOrWhiteSpace(note) ? "" : $"; {note}";
        return (
            $"to {Format(DateTimeUtils.ToLocal(policy.Zone, newStartsAt))} in {newRoomId}{suffix}",
            $"from {Format(DateTimeUtils.ToLocal(policy.Zone, old.StartsAt))} in {old.RoomId}{suffix}");
    }

    private static ScheduleViolation OldConflict(MoveSource old, string rule, string message, BookingPolicy policy) =>
        new(rule, DateTimeUtils.LocalDate(policy.Zone, old.StartsAt), [old.Id], old.LessonIds, message);

    private static string Format(DateTimeOffset local) => local.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
}
