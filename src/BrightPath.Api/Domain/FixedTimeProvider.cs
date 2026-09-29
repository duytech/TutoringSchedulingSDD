namespace BrightPath.Api.Domain;

/// <summary>
/// A clock that never moves. Today is pinned (DECISIONS §1), so the demo and the tests see the same day
/// and the same 4-hour window every run.
/// </summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
