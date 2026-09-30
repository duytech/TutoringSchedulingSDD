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

        var utcNow = clock.GetUtcNow();
        var localDate = date ?? policy.LocalDate(utcNow);
        var daySessions = await reader.DaySessionsAsync(localDate, tutorId, ct);
        var bookingChanges = await reader.ChangesOfAsync(daySessions, ct);
        var moveTargets = await reader.MoveTargetsAsync(daySessions, ct);

        return TutorDaySheet.Build(tutor, localDate, utcNow, daySessions, bookingChanges, policy, moveTargets);
    }
}
