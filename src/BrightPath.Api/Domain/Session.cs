using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrightPath.Api.Domain;

/// <summary>One tutor, one room, one time slot, with one or two attendees.</summary>
public sealed class Session
{
    public Guid Id { get; init; }
    public required string TutorId { get; init; }
    public required string RoomId { get; init; }
    public DateTimeOffset StartsAt { get; init; }
    public DateTimeOffset EndsAt { get; init; }

    /// <summary>A session is active while this is null.</summary>
    public DateTimeOffset? CancelledAt { get; set; }

    public Guid? MovedToSessionId { get; set; }

    /// <summary>Seeded row that overlaps an earlier one; left out of the exclusion constraints.</summary>
    public bool LegacyViolation { get; init; }

    public Tutor Tutor { get; init; } = null!;
    public Room Room { get; init; } = null!;
    public List<Attendee> Attendees { get; init; } = [];
}

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
