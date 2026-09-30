using BrightPath.Api.Domain;
using BrightPath.Api.Seed;

namespace BrightPath.Api.UnitTests.Infrastructure;

/// <summary>The real export through <see cref="SeedPlanner"/>, as the day views read it. No database.</summary>
internal sealed record SeedExport(
    IReadOnlyList<Tutor> Tutors, IReadOnlyList<DaySession> Sessions, IReadOnlyList<BookingChange> Changes)
{
    public static SeedExport Load(BookingPolicy policy)
    {
        var plan = SeedPlanner.Plan(
            SeedCsv.ReadTutors(Path.Combine(SeedCsv.Directory, "tutors.csv")),
            SeedCsv.ReadLessons(Path.Combine(SeedCsv.Directory, "lessons_export.csv")),
            policy);
        var tutors = plan.Tutors.ToDictionary(t => t.Id, t => t.Name);
        var students = plan.Students.ToDictionary(s => s.Id, s => s.Name);
        var sessions = plan.Sessions.Select(s => new DaySession(
            s.Id, s.TutorId, tutors[s.TutorId], s.RoomId, s.StartsAt, s.EndsAt, s.CancelledAt, s.MovedToSessionId,
            s.LegacyViolation,
            s.Attendees.Select(a => new DayAttendee(
                a.Id, a.StudentId, students[a.StudentId], a.SourceLessonId, a.Status, a.CancelledAt, a.CancelledBy,
                a.Chargeable, a.LegacyViolation, a.Note)).ToList())).ToList();

        return new SeedExport(plan.Tutors, sessions, plan.Changes);
    }
}
