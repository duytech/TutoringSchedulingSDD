namespace BrightPath.Application.Tutors;

/// <summary>One session of a tutor's day as GetTutorDay reads it. Built from the database or the seed plan.</summary>
public sealed record GetTutorDayResponse(
    Guid Id,
    string TutorId,
    string TutorName,
    string RoomId,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    DateTimeOffset? CancelledAt,
    Guid? MovedToSessionId,
    bool LegacyViolation,
    IReadOnlyList<GetTutorDayResponse.Attendee> Attendees)
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
