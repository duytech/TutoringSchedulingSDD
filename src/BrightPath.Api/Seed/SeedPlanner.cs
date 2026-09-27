using BrightPath.Api.Domain;

namespace BrightPath.Api.Seed;

public sealed record SeedPlan(
    IReadOnlyList<Tutor> Tutors,
    IReadOnlyList<Student> Students,
    IReadOnlyList<Session> Sessions,
    IReadOnlyList<BookingChange> Changes)
{
    public IEnumerable<Attendee> Attendees => Sessions.SelectMany(s => s.Attendees);
}

/// <summary>
/// Turns the export into entities, as history: every row is kept, and rows that break an overlap rule
/// are flagged <c>legacy_violation</c> instead of fixed (DECISIONS §1, §3). Pure: no I/O, no database.
/// </summary>
public static class SeedPlanner
{
    public static SeedPlan Plan(IEnumerable<TutorRow> tutorRows, IEnumerable<LessonRow> lessonRows, BookingPolicy policy)
    {
        var lessons = lessonRows.ToList();

        var tutors = tutorRows
            .Select(r => new Tutor { Id = r.TutorId, Name = r.TutorName, Subject = r.Subject })
            .ToList();

        // A name is the identity: the export has no student ID.
        var students = lessons
            .Select(r => r.Student)
            .Distinct()
            .Select(name => new Student { Id = Guid.CreateVersion7(), Name = name })
            .ToList();
        var studentIds = students.ToDictionary(s => s.Name, s => s.Id);

        // Rows with the same tutor, room and time are one session: the exam pair has two rows.
        // "Later" means later start, then higher lesson ID, so a tie on time goes to the higher ID.
        var slots = lessons
            .GroupBy(r => (r.Date, r.StartTime, r.DurationMin, r.TutorId, r.Room))
            .Select(g =>
            {
                var startsAt = policy.ToInstant(g.Key.Date, g.Key.StartTime);
                var rows = g.OrderBy(r => r.LessonId, StringComparer.Ordinal).ToList();
                return new Slot(rows, startsAt, startsAt.AddMinutes(g.Key.DurationMin));
            })
            .OrderBy(s => s.StartsAt)
            .ThenBy(s => s.Rows[0].LessonId, StringComparer.Ordinal)
            .ToList();

        var legacySlots = FindLegacySlots(slots);
        var legacyRows = FindLegacyRows(slots);

        var sessions = new List<Session>();
        var changes = new List<BookingChange>();
        foreach (var slot in slots)
        {
            var sessionId = Guid.CreateVersion7();
            var attendees = new List<Attendee>();

            foreach (var row in slot.Rows)
            {
                var cancelledAt = row.CancelledAt?.ToUniversalTime();
                var cancelledBy = cancelledAt is null ? null : CancelledBy.FromNote(row.Note);
                var attendee = new Attendee
                {
                    Id = Guid.CreateVersion7(),
                    SessionId = sessionId,
                    StudentId = studentIds[row.Student],
                    Status = row.Status,
                    CancelledAt = cancelledAt,
                    CancelledBy = cancelledBy,
                    Chargeable = cancelledAt is { } at && policy.IsChargeable(cancelledBy, at, slot.StartsAt),
                    LegacyViolation = legacyRows.Contains(row),
                    SourceLessonId = row.LessonId,
                    Note = NullIfEmpty(row.Note),
                };
                attendees.Add(attendee);

                // Booked rows get no "created" change: the export does not say when they were made.
                if (cancelledAt is { } changedAt)
                {
                    changes.Add(new BookingChange
                    {
                        Id = Guid.CreateVersion7(),
                        SessionId = sessionId,
                        AttendeeId = attendee.Id,
                        Kind = ChangeKind.Cancelled,
                        ChangedAt = changedAt,
                        ChangedBy = cancelledBy,
                        AfterCutoff = policy.IsAfterCutoff(changedAt, slot.StartsAt),
                        Note = attendee.Note,
                    });
                }
            }

            sessions.Add(new Session
            {
                Id = sessionId,
                TutorId = slot.Rows[0].TutorId,
                RoomId = slot.Rows[0].Room,
                StartsAt = slot.StartsAt,
                EndsAt = slot.EndsAt,
                // The last attendee cancelled cancels the session (DECISIONS §3).
                CancelledAt = slot.IsCancelled ? attendees.Max(a => a.CancelledAt) : null,
                LegacyViolation = legacySlots.Contains(slot),
                Attendees = attendees,
            });
        }

        return new SeedPlan(tutors, students, sessions, changes);
    }

    /// <summary>
    /// Sessions left out of ex_sessions_room_slot and ex_sessions_tutor_slot: each active session that
    /// overlaps an earlier covered one in the same room or with the same tutor.
    /// </summary>
    private static HashSet<Slot> FindLegacySlots(List<Slot> slots)
    {
        var covered = new List<Slot>();
        var legacy = new HashSet<Slot>();
        foreach (var slot in slots.Where(s => !s.IsCancelled))
        {
            var clash = covered.Any(c =>
                c.Overlaps(slot) && (c.Rows[0].Room == slot.Rows[0].Room || c.Rows[0].TutorId == slot.Rows[0].TutorId));
            if (clash) legacy.Add(slot);
            else covered.Add(slot);
        }
        return legacy;
    }

    /// <summary>
    /// Attendees left out of ex_attendees_student_slot: each attendee that is not cancelled and whose
    /// student already has an earlier covered attendee at an overlapping time. A no-show holds its slot.
    /// </summary>
    private static HashSet<LessonRow> FindLegacyRows(List<Slot> slots)
    {
        var covered = new List<(string Student, Slot Slot)>();
        var legacy = new HashSet<LessonRow>();
        foreach (var slot in slots)
        {
            foreach (var row in slot.Rows.Where(r => r.Status != AttendeeStatus.Cancelled))
            {
                var clash = covered.Any(c => c.Student == row.Student && c.Slot.Overlaps(slot));
                if (clash) legacy.Add(row);
                else covered.Add((row.Student, slot));
            }
        }
        return legacy;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record Slot(List<LessonRow> Rows, DateTimeOffset StartsAt, DateTimeOffset EndsAt)
    {
        public bool IsCancelled => Rows.All(r => r.Status == AttendeeStatus.Cancelled);

        // Half-open [start, end), the same as the tstzrange in the constraints.
        public bool Overlaps(Slot other) => StartsAt < other.EndsAt && other.StartsAt < EndsAt;
    }
}
