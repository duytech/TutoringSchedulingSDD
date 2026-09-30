using BrightPath.Application.Abstractions;
using BrightPath.Application.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Tutors;

/// <summary>One tutor's day, with what changed after they were told.</summary>
public sealed class GetTutorDayHandler(
    IScheduleReader reader, IReferenceData referenceData, BookingPolicy policy, TimeProvider clock)
{
    /// <summary>The default date is today on the clock.</summary>
    public async Task<Result<TutorDaySheetView>> HandleAsync(string tutorId, DateOnly? date, CancellationToken ct)
    {
        var tutor = await referenceData.FindTutorAsync(tutorId, ct);
        if (tutor is null)
        {
            return new NotFoundError("Tutor not found", $"No tutor {tutorId}.");
        }

        var now = clock.GetUtcNow();
        var day = date ?? policy.LocalDate(now);
        var sessions = await reader.DaySessionsAsync(day, tutorId, ct);
        var changes = await reader.ChangesOfAsync(sessions, ct);
        var moveTargets = await reader.MoveTargetsAsync(sessions, ct);

        return TutorDaySheet.Build(tutor, day, now, sessions, changes, policy, moveTargets);
    }
}
