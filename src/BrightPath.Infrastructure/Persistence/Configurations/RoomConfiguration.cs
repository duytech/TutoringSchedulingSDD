using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrightPath.Infrastructure.Persistence.Configurations;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);

        // Reference data: the centre has six identical rooms (DECISIONS Q4).
        builder.HasData(Enumerable.Range(1, 6).Select(n => new Room { Id = $"R{n}" }));
    }
}
