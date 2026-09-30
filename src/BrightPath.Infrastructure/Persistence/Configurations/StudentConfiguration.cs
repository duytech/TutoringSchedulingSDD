using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrightPath.Infrastructure.Persistence.Configurations;

internal sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.HasKey(s => s.Id);

        // The export has no student ID, so the name is the identity.
        builder.HasIndex(s => s.Name).IsUnique();
    }
}
