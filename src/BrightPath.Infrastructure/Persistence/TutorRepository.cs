using BrightPath.Application.Abstractions;
using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

internal sealed class TutorRepository(BrightPathDbContext db) : ITutorRepository
{
    public Task<Tutor?> FindAsync(string id, CancellationToken ct) =>
        db.Tutors.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct);

    public Task<List<Tutor>> ListAsync(CancellationToken ct) =>
        db.Tutors.AsNoTracking().ToListAsync(ct);
}
