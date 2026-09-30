using BrightPath.Domain;

namespace BrightPath.Application.Abstractions;

/// <summary>Tutors, rooms and students, read only.</summary>
public interface IReferenceData
{
    Task<Tutor?> FindTutorAsync(string id, CancellationToken ct);

    Task<bool> RoomExistsAsync(string id, CancellationToken ct);

    /// <summary>The students among <paramref name="ids"/> that exist. Unknown ids are left out.</summary>
    Task<List<Student>> FindStudentsAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    Task<List<string>> RoomIdsAsync(CancellationToken ct);

    Task<List<Tutor>> TutorsAsync(CancellationToken ct);
}
