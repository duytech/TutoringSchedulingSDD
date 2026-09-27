using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrightPath.Api.Domain;

/// <summary>One student in one session.</summary>
public sealed class Attendee
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }
    public Guid StudentId { get; init; }
    public required string Status { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public bool Chargeable { get; set; }
    public bool LegacyViolation { get; init; }

    /// <summary>Lesson ID from the CSV export (L001…); null for bookings made in the app.</summary>
    public string? SourceLessonId { get; init; }

    public string? Note { get; init; }

    public Session Session { get; init; } = null!;
    public Student Student { get; init; } = null!;
}

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
