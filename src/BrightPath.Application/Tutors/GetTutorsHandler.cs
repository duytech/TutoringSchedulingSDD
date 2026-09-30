using BrightPath.Application.Abstractions;

namespace BrightPath.Application.Tutors;

public sealed record TutorView(string Id, string Name, string Subject);

/// <summary>Every tutor, ordered by id. Reference data: the same on every day.</summary>
public sealed class GetTutorsHandler(IReferenceData referenceData)
{
    public async Task<IReadOnlyList<TutorView>> HandleAsync(CancellationToken ct) =>
        (await referenceData.TutorsAsync(ct))
            .OrderBy(t => t.Id, StringComparer.Ordinal)
            .Select(t => new TutorView(t.Id, t.Name, t.Subject))
            .ToList();
}
