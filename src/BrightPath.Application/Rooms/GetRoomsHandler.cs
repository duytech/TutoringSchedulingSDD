using BrightPath.Application.Abstractions;

namespace BrightPath.Application.Rooms;

public sealed record RoomView(string Id);

/// <summary>Every room, ordered by id. Reference data: the same on every day.</summary>
public sealed class GetRoomsHandler(IReferenceData referenceData)
{
    public async Task<IReadOnlyList<RoomView>> HandleAsync(CancellationToken ct) =>
        (await referenceData.RoomIdsAsync(ct))
            .Order(StringComparer.Ordinal)
            .Select(id => new RoomView(id))
            .ToList();
}
