namespace BrightPath.Infrastructure.Persistence;

/// <summary>Builds the IN list of a CHECK constraint from the allowed values in <c>BrightPath.Domain.Values</c>.</summary>
internal static class SqlList
{
    public static string In(string column, IEnumerable<string> values) =>
        $"{column} IN ({string.Join(", ", values.Select(v => $"'{v}'"))})";
}
