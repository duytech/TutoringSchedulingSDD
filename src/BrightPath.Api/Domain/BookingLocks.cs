using System.Globalization;

namespace BrightPath.Api.Domain;

/// <summary>Keys for the Postgres advisory locks a booking takes. One place, so every caller builds the same key.</summary>
public static class BookingLocks
{
    /// <summary>Bookings for the same tutor and local day take turns, so the daily load cannot be passed in a race.</summary>
    public static string TutorDay(string tutorId, DateOnly date) =>
        $"tutor-day:{tutorId}:{date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
}
