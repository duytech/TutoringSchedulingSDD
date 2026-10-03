using BrightPath.Common;
using BrightPath.Domain;
using BrightPath.Infrastructure.Persistence;

namespace BrightPath.Api.UnitTests;

/// <summary>Which sessions belong to a day: decided once, in the reader's query, not again in the views.</summary>
public sealed class SessionQueriesTests
{
    private static readonly BookingPolicy Policy = TestPolicy.Create();
    private static readonly DateOnly Friday = new(2026, 3, 6);

    [Fact]
    public void A_day_runs_from_its_local_midnight_up_to_the_next_one()
    {
        var thursdayLate = At(Friday.AddDays(-1), "23:59");
        var fridayMidnight = At(Friday, "00:00");
        var fridayLate = At(Friday, "23:59");
        var saturdayMidnight = At(Friday.AddDays(1), "00:00");

        var picked = new[] { thursdayLate, fridayMidnight, fridayLate, saturdayMidnight }
            .AsQueryable()
            .StartingOn(Friday, Policy)
            .ToList();

        Assert.Equal([fridayMidnight, fridayLate], picked);
    }

    private static Session At(DateOnly date, string localTime)
    {
        var startsAt = DateTimeUtils.LocalToUtc(Policy.Zone, date, TimeOnly.Parse(localTime));
        return new Session
        {
            Id = Guid.NewGuid(), TutorId = "T1", RoomId = "R1", StartsAt = startsAt, EndsAt = startsAt.AddHours(1),
        };
    }
}
