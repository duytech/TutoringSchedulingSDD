namespace BrightPath.Application.Sessions;

/// <summary>One session of the day as GetDaySessions reads it. Built from the database or the seed plan.</summary>
public sealed record GetDaySessionsResponse(
    Guid Id,
    string TutorId,
    string TutorName,
    string RoomId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset? CancelledAt,
    Guid? MovedToSessionId,
    bool LegacyViolation,
    IReadOnlyList<GetDaySessionsResponse.Attendee> Attendees)
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
