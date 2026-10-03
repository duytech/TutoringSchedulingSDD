using BrightPath.Application.Abstractions;
using BrightPath.Application.Common;
using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

/// <summary>One session, in the same shape as an item of the day's sessions. The write use cases answer with it too.</summary>
public sealed class GetSessionHandler(ISessionReader reader, IBookingChangeRepository bookingChanges, BookingPolicy policy)
{
    public async Task<Result<SessionView>> HandleAsync(Guid id, CancellationToken ct)
    {
        var view = await LoadAsync(id, ct);
        return view is null ? new NotFoundError("Session not found", $"No session {id}.") : view;
    }

    /// <summary>Null when there is no such session.</summary>
    public async Task<SessionView?> LoadAsync(Guid id, CancellationToken ct)
    {
        var session = await reader.GetSessionAsync(id, ct);
        if (session is null)
        {
            return null;
        }

        var changes = await bookingChanges.ChangesOfAsync([session.Id], ct);
        var moveTargets = await reader.MoveTargetsAsync(session.MovedToSessionId is { } targetId ? [targetId] : [], ct);
        return ToView(session, changes, policy, moveTargets);
    }

    /// <summary><paramref name="moveTargets"/> holds the sessions moved-to sessions point at, with UTC start times.</summary>
    private static SessionView ToView(
        GetSessionResponse s,
        IEnumerable<BookingChange> changes,
        BookingPolicy policy,
        IReadOnlyDictionary<Guid, MovedToView>? moveTargets)
    {
        var changeViews = SessionChanges.Views(changes, policy);

        return new SessionView(
            s.Id,
            s.TutorId,
            s.TutorName,
            s.RoomId,
            DateTimeUtils.ToLocal(policy.Zone, s.StartsAt),
            DateTimeUtils.ToLocal(policy.Zone, s.EndsAt),
            DateTimeUtils.ToLocal(policy.Zone, s.CancelledAt),
            s.MovedToSessionId,
            s.MovedToSessionId is { } to && moveTargets?.GetValueOrDefault(to) is { } target
                ? target with { StartsAt = DateTimeUtils.ToLocal(policy.Zone, target.StartsAt) }
                : null,
            s.LegacyViolation,
            s.Attendees
                .OrderBy(a => a.LessonId is null)
                .ThenBy(a => a.LessonId, StringComparer.Ordinal)
                .ThenBy(a => a.StudentName, StringComparer.Ordinal)
                .Select(a => new SessionAttendeeView(
                    a.Id, a.StudentId, a.StudentName, a.LessonId, a.Status,
                    DateTimeUtils.ToLocal(policy.Zone, a.CancelledAt), a.CancelledBy, a.Chargeable,
                    a.LegacyViolation, a.Note))
                .ToList(),
            changeViews);
    }
}
