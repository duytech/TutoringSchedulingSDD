using BrightPath.Application.Abstractions;
using BrightPath.Application.Schedule;
using BrightPath.Domain;

namespace BrightPath.Api.UnitTests.Fakes;

/// <summary>
/// The Application ports in memory, so a use case runs with no database. The day holds no other session, so every
/// slot is free unless a test makes the next save fail.
/// </summary>
internal sealed class InMemoryBooking : IReferenceData, ISessionRepository, IScheduleReader, IBookingLocks, IUnitOfWork
{
    public List<Tutor> Tutors { get; } = [new() { Id = "T2", Name = "Minh Quan", Subject = "English" }];
    public List<string> RoomIds { get; } = ["R1", "R2", "R3", "R4", "R5", "R6"];
    public List<Student> Students { get; } = [new() { Id = Guid.CreateVersion7(), Name = "Vu Ha My" }];
    public List<Session> Sessions { get; } = [];
    public List<BookingChange> Changes { get; } = [];
    public int Commits { get; private set; }

    /// <summary>When set, the next save throws it, as a race refused by the database would.</summary>
    public SlotTakenException? FailNextSave { get; set; }

    // IReferenceData

    public Task<Tutor?> FindTutorAsync(string id, CancellationToken ct) =>
        Task.FromResult(Tutors.SingleOrDefault(t => t.Id == id));

    public Task<bool> RoomExistsAsync(string id, CancellationToken ct) => Task.FromResult(RoomIds.Contains(id));

    public Task<List<Student>> FindStudentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        Task.FromResult(Students.Where(s => ids.Contains(s.Id)).ToList());

    public Task<List<string>> RoomIdsAsync(CancellationToken ct) => Task.FromResult(RoomIds.ToList());

    public Task<List<Tutor>> TutorsAsync(CancellationToken ct) => Task.FromResult(Tutors.ToList());

    // ISessionRepository

    public Task<Session?> GetForUpdateAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Sessions.SingleOrDefault(s => s.Id == id));

    public Task<List<RuleSession>> ActiveOnAsync(DateOnly date, CancellationToken ct) =>
        Task.FromResult(new List<RuleSession>());

    public Task<List<RuleSession>> ActiveBetweenAsync(DateOnly? from, DateOnly? to, CancellationToken ct) =>
        Task.FromResult(new List<RuleSession>());

    public void Add(Session session) => Sessions.Add(session);

    public void AddChanges(params BookingChange[] changes) => Changes.AddRange(changes);

    // IScheduleReader

    public Task<List<DaySession>> DaySessionsAsync(DateOnly date, string? tutorId, CancellationToken ct) =>
        Task.FromResult(new List<DaySession>());

    public Task<DaySession?> DaySessionAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Sessions.Where(s => s.Id == id).Select(ToDaySession).SingleOrDefault());

    public Task<List<BookingChange>> ChangesOfAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken ct) =>
        Task.FromResult(Changes.Where(c => sessionIds.Contains(c.SessionId)).ToList());

    public Task<Dictionary<Guid, MovedToView>> MoveTargetsAsync(IReadOnlyCollection<Guid> targetIds, CancellationToken ct) =>
        Task.FromResult(new Dictionary<Guid, MovedToView>());

    // IBookingLocks

    public Task TutorDayAsync(string tutorId, DateOnly date, CancellationToken ct) => Task.CompletedTask;

    // IUnitOfWork

    public Task<ITransaction> BeginTransactionAsync(CancellationToken ct) =>
        Task.FromResult<ITransaction>(new Transaction(this));

    public Task SaveChangesAsync(CancellationToken ct)
    {
        if (FailNextSave is { } failure)
        {
            FailNextSave = null;
            throw failure;
        }

        return Task.CompletedTask;
    }

    private DaySession ToDaySession(Session s) => new(
        s.Id,
        s.TutorId,
        Tutors.Single(t => t.Id == s.TutorId).Name,
        s.RoomId,
        s.StartsAt,
        s.EndsAt,
        s.CancelledAt,
        s.MovedToSessionId,
        s.LegacyViolation,
        s.Attendees
            .Select(a => new DayAttendee(
                a.Id, a.StudentId, Students.Single(st => st.Id == a.StudentId).Name, a.SourceLessonId, a.Status,
                a.CancelledAt, a.CancelledBy, a.Chargeable, a.LegacyViolation, a.Note))
            .ToList());

    private sealed class Transaction(InMemoryBooking store) : ITransaction
    {
        public Task CommitAsync(CancellationToken ct)
        {
            store.Commits++;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
