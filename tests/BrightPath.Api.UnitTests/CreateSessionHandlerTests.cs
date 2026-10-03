using BrightPath.Api.UnitTests.Fakes;
using BrightPath.Application.Abstractions;
using BrightPath.Application.Common;
using BrightPath.Application.Sessions;
using BrightPath.Domain;
using BrightPath.Infrastructure.Time;

namespace BrightPath.Api.UnitTests;

/// <summary>The create use case run on in-memory ports: no database, no HTTP.</summary>
public sealed class CreateSessionHandlerTests
{
    private static readonly BookingPolicy Policy = TestPolicy.Create();
    private static readonly DateTimeOffset PinnedNow = DateTimeOffset.Parse("2026-03-06T10:00:00+07:00");

    private readonly InMemoryBooking _store = new();

    [Fact]
    public async Task Bad_input_is_refused_field_by_field()
    {
        var request = new CreateSessionRequest("T2", "R4", "2026-03-07T13:00:00", 45, [_store.Students[0].Id]);

        var result = await Handler().HandleAsync(request, CancellationToken.None);

        var error = Assert.IsType<ValidationError>(result.Error);
        Assert.Equal(["startsAt", "durationMin"], error.Errors.Keys);
        Assert.Empty(_store.Sessions);
    }

    [Fact]
    public async Task A_free_slot_is_booked_with_a_created_change()
    {
        var result = await Handler().HandleAsync(Request(), CancellationToken.None);

        Assert.Null(result.Error);
        var session = Assert.Single(_store.Sessions);
        Assert.Equal(session.Id, result.Value!.Id);
        Assert.Equal(DateTimeOffset.Parse("2026-03-07T13:00:00+07:00"), session.StartsAt);
        Assert.Equal(ChangeKind.Created, Assert.Single(_store.Changes).Kind);
        Assert.Equal(1, _store.Commits);
    }

    [Fact]
    public async Task A_slot_taken_in_a_race_is_a_room_overlap_conflict()
    {
        _store.FailNextSave = new SlotTakenException(SlotKind.Room, new InvalidOperationException("23P01"));

        var result = await Handler().HandleAsync(Request(), CancellationToken.None);

        var error = Assert.IsType<ConflictError>(result.Error);
        var conflict = Assert.Single(error.Conflicts);
        Assert.Equal(RuleCodes.RoomOverlap, conflict.Rule);
        Assert.Equal("R4 was booked by someone else at the same moment.", conflict.Message);
        Assert.Equal(0, _store.Commits);
    }

    private CreateSessionRequest Request() =>
        new("T2", "R4", "2026-03-07T13:00:00+07:00", 60, [_store.Students[0].Id]);

    private CreateSessionHandler Handler()
    {
        var clock = new FixedTimeProvider(PinnedNow);
        return new CreateSessionHandler(
            _store, _store, _store, _store, new GetSessionHandler(_store, Policy), Policy, clock);
    }
}
