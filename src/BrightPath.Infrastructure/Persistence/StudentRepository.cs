using BrightPath.Application.Abstractions;
using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class StudentRepository(BrightPathDbContext db) : IStudentRepository
{
    public Task<List<Student>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        db.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(ct);
}
