namespace BrightPath.Domain;

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
