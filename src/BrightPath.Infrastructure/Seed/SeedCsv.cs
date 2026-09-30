using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace BrightPath.Infrastructure.Seed;

/// <summary>One line of lessons_export.csv. One line is one attendee.</summary>
public sealed record LessonRow(
    string LessonId,
    DateOnly Date,
    TimeOnly StartTime,
    int DurationMin,
    string Student,
    string TutorId,
    string Room,
    string Status,
    DateTimeOffset? CancelledAt,
    string? Note);

/// <summary>One line of tutors.csv. The phone column is not read: nothing needs it.</summary>
public sealed record TutorRow(string TutorId, string TutorName, string Subject);

public static class SeedCsv
{
    public static string Directory => Path.Combine(AppContext.BaseDirectory, "Seed");

    public static List<LessonRow> ReadLessons(string path) => Read<LessonRow>(path);

    public static List<TutorRow> ReadTutors(string path) => Read<TutorRow>(path);

    // Matches "lesson_id" to LessonId by dropping underscores and case on both sides.
    private static readonly CsvConfiguration Config = new(CultureInfo.InvariantCulture)
    {
        PrepareHeaderForMatch = args => args.Header.Replace("_", "", StringComparison.Ordinal).ToLowerInvariant(),
    };

    private static List<T> Read<T>(string path)
    {
        using var reader = new StreamReader(path);
        using var csv = new CsvReader(reader, Config);
        return csv.GetRecords<T>().ToList();
    }
}
