using System.Globalization;
using System.Text.RegularExpressions;

namespace BrightPath.Application.Sessions;

/// <summary>Input rules shared by create, cancel and move, so each field is checked and worded the same way.</summary>
internal static partial class SessionInput
{
    public static readonly int[] Durations = [60, 90];
    public const int MaxNoteLength = 500;

    public const string StartsAtError =
        "A local time with its offset is required, e.g. 2026-03-07T13:00:00+07:00.";

    public const string DurationError = "Must be 60 or 90.";

    public static string NoteError => $"At most {MaxNoteLength} characters.";

    /// <summary><c>startsAt</c> is a string so a value without an offset can be refused instead of guessed.</summary>
    public static bool TryParseWithOffset(string? value, out DateTimeOffset result)
    {
        result = default;
        return value is not null
            && OffsetSuffix().IsMatch(value)
            && DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out result);
    }

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex OffsetSuffix();
}
