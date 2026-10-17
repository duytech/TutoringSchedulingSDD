using BrightPath.Application.Abstractions;
using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

/// <summary>Loads one session in the same shape as an item of the day's sessions. The write use cases answer with it too.</summary>
public sealed class SessionViewLoader(ISessionReader sessionReader, IBookingChangeRepository bookingChangeRepository, BookingPolicy bookingPolicy)
{
    /// <summary>Null when there is no such session.</summary>
    public async Task<SessionView?> LoadAsync(Guid id, CancellationToken ct)
    {
        var session = await sessionReader.GetSessionAsync(id, ct);
        if (session is null)
        {
            return null;
        }

        var changes = await bookingChangeRepository.ListChangesAsync([session.Id], ct);
        var moveTargets = await sessionReader.GetMoveTargetsAsync(session.MovedToSessionId is { } targetId ? [targetId] : [], ct);
        return ToView(session, changes, bookingPolicy, moveTargets);
    }

    /// <summary><paramref name="moveTargets"/> holds the sessions moved-to sessions point at, with UTC start times.</summary>
    private static SessionView ToView(
        GetSessionResponse s,
        IEnumerable<BookingChange> changes,
        BookingPolicy bookingPolicy,
        IReadOnlyDictionary<Guid, MovedToView>? moveTargets)
    {
        var changeViews = BookingChangeViews.From(changes, bookingPolicy);

        return new SessionView(
            s.Id,
            s.TutorId,
            s.TutorName,
            s.RoomId,
            DateTimeUtils.ToLocal(bookingPolicy.Zone, s.StartsAt),
            DateTimeUtils.ToLocal(bookingPolicy.Zone, s.EndsAt),
            DateTimeUtils.ToLocal(bookingPolicy.Zone, s.CancelledAt),
            s.MovedToSessionId,
            s.MovedToSessionId is { } to && moveTargets?.GetValueOrDefault(to) is { } target
                ? target with { StartsAt = DateTimeUtils.ToLocal(bookingPolicy.Zone, target.StartsAt) }
                : null,
            s.LegacyViolation,
            s.Attendees
                .OrderBy(a => a.LessonId is null).ThenBy(a => a.LessonId, StringComparer.Ordinal).ThenBy(a => a.StudentName, StringComparer.Ordinal)
                .Select(a => new SessionAttendeeView(
                    a.Id, a.StudentId, a.StudentName, a.LessonId, a.Status,
                    DateTimeUtils.ToLocal(bookingPolicy.Zone, a.CancelledAt), a.CancelledBy, a.Chargeable,
                    a.LegacyViolation, a.Note))
                .ToList(),
            changeViews);
    }
}
