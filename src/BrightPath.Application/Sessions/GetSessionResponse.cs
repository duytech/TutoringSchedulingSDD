namespace BrightPath.Application.Sessions;

/// <summary>One session as GetSession reads it, by id.</summary>
public sealed record GetSessionResponse(
    Guid Id,
    string TutorId,
    string TutorName,
    string RoomId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset? CancelledAt,
    Guid? MovedToSessionId,
    bool LegacyViolation,
    IReadOnlyList<GetSessionResponse.Attendee> Attendees)
{
    public sealed record Attendee(
        Guid Id,
        Guid StudentId,
        string StudentName,
        string? LessonId,
        string Status,
        DateTimeOffset? CancelledAt,
        string? CancelledBy,
        bool Chargeable,
        bool LegacyViolation,
        string? Note);
}
