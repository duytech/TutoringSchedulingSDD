using BrightPath.Domain;

namespace BrightPath.Application.Abstractions;

/// <summary>Tutors, read only.</summary>
public interface ITutorRepository
{
    Task<Tutor?> FindAsync(string id, CancellationToken ct);

    Task<List<Tutor>> ListAsync(CancellationToken ct);
}
