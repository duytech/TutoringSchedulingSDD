namespace BrightPath.Domain;

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

    /// <summary>
    /// The export has no "cancelled by" column, but the note says it ("family cancelled", "tutor sick").
    /// Returns the first value found in the note, or null.
    /// </summary>
    public static string? FromNote(string? note) =>
        note is null
            ? null
            : All.Select(v => (Value: v, Index: note.IndexOf(v, StringComparison.OrdinalIgnoreCase)))
                .Where(m => m.Index >= 0)
                .OrderBy(m => m.Index)
                .Select(m => m.Value)
                .FirstOrDefault();
}

public static class ChangeKind
{
    public const string Created = "created";
    public const string Cancelled = "cancelled";
    public const string Moved = "moved";

    public static readonly string[] All = [Created, Cancelled, Moved];
}
