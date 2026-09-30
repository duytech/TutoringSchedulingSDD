using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrightPath.Infrastructure.Persistence.Configurations;

internal sealed class BookingChangeConfiguration : IEntityTypeConfiguration<BookingChange>
{
    public void Configure(EntityTypeBuilder<BookingChange> builder)
    {
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_booking_changes_kind", SqlList.In("kind", ChangeKind.All));
            t.HasCheckConstraint(
                "ck_booking_changes_changed_by",
                $"changed_by IS NULL OR {SqlList.In("changed_by", CancelledBy.All)}");
        });

        builder.HasKey(c => c.Id);

        builder.HasOne<Session>().WithMany().HasForeignKey(c => c.SessionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Attendee>().WithMany().HasForeignKey(c => c.AttendeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(c => new { c.SessionId, c.ChangedAt });
    }
}
