using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrightPath.Infrastructure.Persistence.Configurations;

internal sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_sessions_duration",
            "ends_at - starts_at IN (interval '60 minutes', interval '90 minutes')"));

        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.Tutor).WithMany().HasForeignKey(s => s.TutorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Room).WithMany().HasForeignKey(s => s.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Session>().WithMany().HasForeignKey(s => s.MovedToSessionId).OnDelete(DeleteBehavior.Restrict);
    }
}
