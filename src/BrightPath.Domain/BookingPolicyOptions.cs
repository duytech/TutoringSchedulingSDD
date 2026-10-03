namespace BrightPath.Domain;

/// <summary>Policy numbers the owner may still change, so they live in config (DECISIONS §1).</summary>
public sealed class BookingPolicyOptions
{
    public const string Section = "BookingPolicy";

    /// <summary>IANA zone the centre's local times are in.</summary>
    public required string TimeZone { get; init; }

    /// <summary>Tomorrow's schedule is final at this local time today.</summary>
    public required TimeOnly CutoffLocalTime { get; init; }

    /// <summary>A family cancellation closer than this to the start is chargeable.</summary>
    public required TimeSpan LateCancellationWindow { get; init; }

    /// <summary>Counts sessions, not students: an exam pair is one (Q1).</summary>
    public required int MaxSessionsPerTutorPerDay { get; init; }

    public required int MaxAttendeesPerSession { get; init; }

    public required DayOfWeek[] ClosedDays { get; init; }

    public required TimeOnly OpensAt { get; init; }

    public required TimeOnly ClosesAt { get; init; }
}
