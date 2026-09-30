using BrightPath.Application.Abstractions;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

/// <summary>The conflict shown when a race got past the code checks and the database refused the slot.</summary>
internal static class RaceConflict
{
    public static ScheduleViolation For(SlotKind slot, RuleSession candidate, DateOnly date)
    {
        var (rule, what) = slot switch
        {
            SlotKind.Room => (RuleCodes.RoomOverlap, candidate.RoomId),
            SlotKind.Tutor => (RuleCodes.TutorOverlap, candidate.TutorId),
            _ => (RuleCodes.StudentOverlap, "A student"),
        };
        return new ScheduleViolation(
            rule, date, [candidate.Id], [], $"{what} was booked by someone else at the same moment.");
    }
}
