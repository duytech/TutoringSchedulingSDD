using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrightPath.Api.Domain;

public sealed class Tutor
{
    public required string Id { get; init; }
    public required string Name { get; set; }
    public required string Subject { get; set; }
}

internal sealed class TutorConfiguration : IEntityTypeConfiguration<Tutor>
{
    public void Configure(EntityTypeBuilder<Tutor> builder)
    {
        builder.HasKey(t => t.Id);
    }
}
