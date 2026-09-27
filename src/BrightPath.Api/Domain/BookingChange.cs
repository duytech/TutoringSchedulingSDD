using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrightPath.Api.Domain;

/// <summary>Append-only record of a change, so nothing the tutor was told is silently overwritten.</summary>
public sealed class BookingChange
{
    public Guid Id { get; init; }
    public Guid SessionId { get; init; }

    /// <summary>Null for a change to the whole session.</summary>
    public Guid? AttendeeId { get; init; }

    public required string Kind { get; init; }
    public DateTimeOffset ChangedAt { get; init; }
    public string? ChangedBy { get; init; }

    /// <summary>True when the change happened after 16:00 on the day before the lesson.</summary>
    public bool AfterCutoff { get; init; }

    public string? Note { get; init; }
}

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
