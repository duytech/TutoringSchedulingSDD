using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrightPath.Infrastructure.Persistence.Configurations;

internal sealed class TutorConfiguration : IEntityTypeConfiguration<Tutor>
{
    public void Configure(EntityTypeBuilder<Tutor> builder)
    {
        builder.HasKey(t => t.Id);
    }
}
