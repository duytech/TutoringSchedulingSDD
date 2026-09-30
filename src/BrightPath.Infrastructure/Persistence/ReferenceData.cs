using BrightPath.Application.Abstractions;
using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class ReferenceData(BrightPathDbContext db) : IReferenceData
{
    public Task<Tutor?> FindTutorAsync(string id, CancellationToken ct) =>
        db.Tutors.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);

    public Task<bool> RoomExistsAsync(string id, CancellationToken ct) =>
        db.Rooms.AnyAsync(r => r.Id == id, ct);

    public Task<List<Student>> FindStudentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(ct);

    public Task<List<string>> RoomIdsAsync(CancellationToken ct) =>
        db.Rooms.AsNoTracking().Select(r => r.Id).ToListAsync(ct);

    public Task<List<Tutor>> TutorsAsync(CancellationToken ct) =>
        db.Tutors.AsNoTracking().ToListAsync(ct);
}
