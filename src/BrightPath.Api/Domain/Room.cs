using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrightPath.Api.Domain;

public sealed class Room
{
    public required string Id { get; init; }
}

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);

        // Reference data: the centre has six identical rooms (DECISIONS Q4).
        builder.HasData(Enumerable.Range(1, 6).Select(n => new Room { Id = $"R{n}" }));
    }
}
