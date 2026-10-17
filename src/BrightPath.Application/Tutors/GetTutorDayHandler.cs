using BrightPath.Application.Abstractions;
using BrightPath.Application.Common;
using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Tutors;

/// <summary>One tutor's day, with what changed after they were told.</summary>
public sealed class GetTutorDayHandler(
    ISessionReader sessionReader,
    IBookingChangeRepository bookingChangeRepository,
    ITutorRepository tutorRepository,
    BookingPolicy bookingPolicy,
    TimeProvider timeProvider)
{
    /// <summary>The default date is today on the clock.</summary>
    public async Task<Result<TutorDaySheetView>> HandleAsync(string tutorId, DateOnly? date, CancellationToken ct)
    {
        var tutor = await tutorRepository.FindAsync(tutorId, ct);
        if (tutor is null)
        {
            return new NotFoundError("Tutor not found", $"No tutor {tutorId}.");
        }

        var utcNow = timeProvider.GetUtcNow();
        var localDate = date ?? DateTimeUtils.GetLocalDate(bookingPolicy.Zone, utcNow);
        var sessions = await sessionReader.GetDaySessionsByTutorAsync(localDate, tutorId, ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var targetIds = sessions.Select(s => s.MovedToSessionId).OfType<Guid>().ToList();
        var changes = await bookingChangeRepository.ListChangesAsync(sessionIds, ct);
        var moveTargets = await sessionReader.GetMoveTargetsAsync(targetIds, ct);

        return TutorDaySheet.Build(tutor, localDate, utcNow, sessions, changes, bookingPolicy, moveTargets);
    }
}
