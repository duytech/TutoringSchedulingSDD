namespace BrightPath.Application.Abstractions;

/// <summary>Rooms, read only.</summary>
public interface IRoomRepository
{
    Task<bool> ExistsAsync(string roomId, CancellationToken ct);

    Task<List<string>> ListIdsAsync(CancellationToken ct);
}
