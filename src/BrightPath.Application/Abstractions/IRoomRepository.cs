namespace BrightPath.Application.Abstractions;

/// <summary>Rooms, read only.</summary>
public interface IRoomRepository
{
    Task<bool> ExistsAsync(string id, CancellationToken ct);

    Task<List<string>> ListIdsAsync(CancellationToken ct);
}
