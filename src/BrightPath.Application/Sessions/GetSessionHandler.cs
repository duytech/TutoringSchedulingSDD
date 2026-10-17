using BrightPath.Application.Common;

namespace BrightPath.Application.Sessions;

/// <summary>One session, in the same shape as an item of the day's sessions.</summary>
public sealed class GetSessionHandler(SessionViewLoader sessionViewLoader)
{
    public async Task<Result<SessionView>> HandleAsync(Guid sessionId, CancellationToken ct)
    {
        var view = await sessionViewLoader.LoadAsync(sessionId, ct);
        return view is null ? new NotFoundError("Session not found", $"No session {sessionId}.") : view;
    }
}
