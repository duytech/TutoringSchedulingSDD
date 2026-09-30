namespace BrightPath.Application.Abstractions;

/// <summary>Locks held until the open transaction ends.</summary>
public interface IBookingLocks
{
    /// <summary>
    /// Tutor load is a count, so no constraint can guard it. Bookings for the same tutor and day take turns,
    /// and each one reads the day only after the one before has committed.
    /// </summary>
    Task TutorDayAsync(string tutorId, DateOnly date, CancellationToken ct);
}
