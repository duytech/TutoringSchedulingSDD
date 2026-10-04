using BrightPath.Domain;

namespace BrightPath.Application.Abstractions;

/// <summary>Students, read only.</summary>
public interface IStudentRepository
{
    /// <summary>The students among <paramref name="ids"/> that exist. Unknown ids are left out.</summary>
    Task<List<Student>> FindManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);
}
