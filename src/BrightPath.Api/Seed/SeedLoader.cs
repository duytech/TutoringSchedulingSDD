using BrightPath.Api.Data;
using BrightPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api.Seed;

public static class SeedLoader
{
    /// <summary>
    /// Loads the exported week into an empty database. Does nothing once any session exists,
    /// so a restart never seeds twice.
    /// </summary>
    public static async Task SeedAsync(BrightPathDbContext db, BookingPolicy policy, ILogger logger)
    {
        if (await db.Sessions.AnyAsync())
        {
            return;
        }

        var plan = SeedPlanner.Plan(
            SeedCsv.ReadTutors(Path.Combine(SeedCsv.Directory, "tutors.csv")),
            SeedCsv.ReadLessons(Path.Combine(SeedCsv.Directory, "lessons_export.csv")),
            policy);

        db.Tutors.AddRange(plan.Tutors);
        db.Students.AddRange(plan.Students);
        db.Sessions.AddRange(plan.Sessions);
        db.BookingChanges.AddRange(plan.Changes);

        // One SaveChanges is one transaction: the week loads whole or not at all. If a legacy_violation
        // flag were missing, an exclusion constraint would refuse the insert here.
        await db.SaveChangesAsync();

        logger.LogInformation(
            "Seeded {Tutors} tutors, {Students} students, {Sessions} sessions, {Attendees} attendees, " +
            "{Changes} booking changes; legacy_violation on {LegacySessions} session(s) and {LegacyAttendees} attendee(s)",
            plan.Tutors.Count,
            plan.Students.Count,
            plan.Sessions.Count,
            plan.Attendees.Count(),
            plan.Changes.Count,
            plan.Sessions.Count(s => s.LegacyViolation),
            plan.Attendees.Count(a => a.LegacyViolation));
    }
}
