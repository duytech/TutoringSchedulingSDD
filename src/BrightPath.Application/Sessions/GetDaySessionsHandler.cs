using BrightPath.Application.Abstractions;
using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Application.Sessions;

public sealed record DaySessionsView(
    DateOnly Date,
    DateTimeOffset Now,
    IReadOnlyList<SessionView> Sessions);

/// <summary>One day's sessions.</summary>
public sealed class GetDaySessionsHandler(
    ISessionReader sessionReader, IBookingChangeRepository bookingChangeRepository, BookingPolicy bookingPolicy, TimeProvider timeProvider)
{
    /// <summary>The default date is today on the clock.</summary>
    public async Task<DaySessionsView> HandleAsync(DateOnly? date, CancellationToken ct)
    {
        var utcNow = timeProvider.GetUtcNow();
        var localDate = date ?? DateTimeUtils.GetLocalDate(bookingPolicy.Zone, utcNow);
        var sessions = await sessionReader.GetDaySessionsAsync(localDate, ct);
        var sessionIds = sessions.Select(s => s.Id).ToList();
        var targetIds = sessions.Select(s => s.MovedToSessionId).OfType<Guid>().ToList();
        var changes = await bookingChangeRepository.ListChangesAsync(sessionIds, ct);
        var moveTargets = await sessionReader.GetMoveTargetsAsync(targetIds, ct);

        var views = DaySessions.Build(sessions, changes, bookingPolicy, moveTargets);
        return new DaySessionsView(localDate, DateTimeUtils.ToLocal(bookingPolicy.Zone, utcNow), views);
    }
}
