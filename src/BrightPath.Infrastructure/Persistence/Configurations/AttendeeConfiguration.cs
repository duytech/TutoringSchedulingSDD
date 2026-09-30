using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrightPath.Infrastructure.Persistence.Configurations;

internal sealed class AttendeeConfiguration : IEntityTypeConfiguration<Attendee>
{
    public void Configure(EntityTypeBuilder<Attendee> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_attendees_status", SqlList.In("status", AttendeeStatus.All));
            t.HasCheckConstraint(
                "ck_attendees_cancelled_by",
                $"cancelled_by IS NULL OR {SqlList.In("cancelled_by", CancelledBy.All)}");
            t.HasCheckConstraint(
                "ck_attendees_cancelled_consistent",
                $"(status = '{AttendeeStatus.Cancelled}') = (cancelled_at IS NOT NULL)");
        });

        builder.HasKey(a => a.Id);

        builder.HasOne(a => a.Session).WithMany(s => s.Attendees).HasForeignKey(a => a.SessionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(a => a.Student).WithMany().HasForeignKey(a => a.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.SessionId, a.StudentId }).IsUnique();
        builder.HasIndex(a => a.SourceLessonId).IsUnique();
    }
}
