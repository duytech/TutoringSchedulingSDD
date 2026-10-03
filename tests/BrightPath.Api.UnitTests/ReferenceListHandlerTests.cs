using BrightPath.Api.UnitTests.Fakes;
using BrightPath.Application.Rooms;
using BrightPath.Application.Tutors;
using BrightPath.Domain;

namespace BrightPath.Api.UnitTests;

/// <summary>The room and tutor lists on in-memory ports: no database, no HTTP.</summary>
public sealed class ReferenceListHandlerTests
{
    private readonly InMemoryBooking _store = new();

    [Fact]
    public async Task Rooms_come_ordered_by_id()
    {
        _store.RoomIds.Clear();
        _store.RoomIds.AddRange(["R3", "R10", "R1", "R2"]);

        var rooms = await new GetRoomsHandler(_store).HandleAsync(CancellationToken.None);

        // Ordinal, like the day's sessions ordering: R10 sorts before R2.
        Assert.Equal(["R1", "R10", "R2", "R3"], rooms.Select(r => r.Id));
    }

    [Fact]
    public async Task Tutors_come_ordered_by_id_with_name_and_subject()
    {
        _store.Tutors.Clear();
        _store.Tutors.AddRange(
        [
            new Tutor { Id = "T3", Name = "Le Thu", Subject = "Physics" },
            new Tutor { Id = "T1", Name = "Ngoc Anh", Subject = "Maths" },
        ]);

        var tutors = await new GetTutorsHandler(_store).HandleAsync(CancellationToken.None);

        Assert.Equal(
            [new TutorView("T1", "Ngoc Anh", "Maths"), new TutorView("T3", "Le Thu", "Physics")],
            tutors);
    }
}
