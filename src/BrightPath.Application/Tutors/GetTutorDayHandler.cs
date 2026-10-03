using BrightPath.Application.Abstractions;
using BrightPath.Application.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Tutors;

/// <summary>One tutor's day, with what changed after they were told.</summary>
public sealed class GetTutorDayHandler(
    ISessionReader reader, IReferenceData referenceData, BookingPolicy policy, TimeProvider clock)
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
        var localDate = date ?? DateTimeUtils.LocalDate(policy.Zone, utcNow);
        var sessions = await reader.GetTutorDayAsync(localDate, tutorId, ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var targetIds = sessions.Select(s => s.MovedToSessionId).OfType<Guid>().ToList();
        var bookingChanges = await reader.ChangesOfAsync(sessionIds, ct);
        var moveTargets = await reader.MoveTargetsAsync(targetIds, ct);

        return TutorDaySheet.Build(tutor, localDate, utcNow, sessions, bookingChanges, policy, moveTargets);
    }
}
