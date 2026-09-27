namespace BrightPath.Api.Domain;

// Allowed values for the text columns. The CHECK constraints are built from the same lists,
// so the database and the code cannot drift apart.

public static class AttendeeStatus
{
    public const string Booked = "booked";
    public const string Cancelled = "cancelled";
    public const string NoShow = "no_show";

    public static readonly string[] All = [Booked, Cancelled, NoShow];
}

public static class CancelledBy
{
    public const string Family = "family";
    public const string Tutor = "tutor";
    public const string Centre = "centre";

    public static readonly string[] All = [Family, Tutor, Centre];
}

public static class ChangeKind
{
    public const string Created = "created";
    public const string Cancelled = "cancelled";
    public const string Moved = "moved";

    public static readonly string[] All = [Created, Cancelled, Moved];
}

internal static class SqlList
{
    public static string In(string column, IEnumerable<string> values) =>
        $"{column} IN ({string.Join(", ", values.Select(v => $"'{v}'"))})";
}
