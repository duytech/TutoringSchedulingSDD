namespace BrightPath.Domain;

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
