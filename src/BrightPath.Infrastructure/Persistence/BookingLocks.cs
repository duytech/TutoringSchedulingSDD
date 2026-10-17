using System.Globalization;

namespace BrightPath.Infrastructure.Persistence;

/// <summary>Keys for the Postgres advisory locks a booking takes. One place, so every caller builds the same key.</summary>
public static class BookingLocks
{
    /// <summary>Builds the advisory lock key for one tutor's local day, e.g. <c>tutor-day:T1:2026-03-17</c>.</summary>
    public static string BuildTutorDayLockKey(string tutorId, DateOnly date) =>
        $"tutor-day:{tutorId}:{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
}
