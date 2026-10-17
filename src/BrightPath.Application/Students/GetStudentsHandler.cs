using BrightPath.Application.Abstractions;

namespace BrightPath.Application.Students;

public sealed record StudentView(Guid Id, string Name);

/// <summary>Every student, ordered by name, then id. Reference data: the same on every day.</summary>
public sealed class GetStudentsHandler(IStudentRepository studentRepository)
{
    public async Task<IReadOnlyList<StudentView>> HandleAsync(CancellationToken ct) =>
        (await studentRepository.ListAsync(ct))
            .OrderBy(student => student.Name, StringComparer.Ordinal).ThenBy(student => student.Id)
            .Select(student => new StudentView(student.Id, student.Name))
            .ToList();
}
