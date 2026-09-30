namespace BrightPath.Domain;

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
