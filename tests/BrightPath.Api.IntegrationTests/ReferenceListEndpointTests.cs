using BrightPath.Api.IntegrationTests.Infrastructure;
using BrightPath.Application.Tutors;

namespace BrightPath.Api.IntegrationTests;

/// <summary>The rooms and tutors over HTTP, as seeded. Read only.</summary>
[Collection(ApiCollection.Name)]
public sealed class ReferenceListEndpointTests(BrightPathApiFactory factory)
{
    [Fact]
    public async Task Rooms_lists_the_six_rooms_in_order()
    {
        var rooms = await ApiCalls.Rooms(factory.CreateClient());

        Assert.Equal(["R1", "R2", "R3", "R4", "R5", "R6"], rooms.Select(r => r.Id));
    }

    [Fact]
    public async Task Tutors_lists_the_seeded_tutors_in_order()
    {
        var tutors = await ApiCalls.Tutors(factory.CreateClient());

        Assert.Equal(
            [
                new TutorView("T1", "Ngoc Anh", "Maths"),
                new TutorView("T2", "Pham Duc", "English"),
                new TutorView("T3", "Le Thu", "Physics"),
            ],
            tutors);
    }
}
