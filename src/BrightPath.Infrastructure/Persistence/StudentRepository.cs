using BrightPath.Application.Abstractions;
using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class StudentRepository(BrightPathDbContext dbContext) : IStudentRepository
{
    public Task<List<Student>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        dbContext.Students.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(ct);

    public Task<List<Student>> ListAsync(CancellationToken ct) =>
        dbContext.Students.AsNoTracking().ToListAsync(ct);
}
