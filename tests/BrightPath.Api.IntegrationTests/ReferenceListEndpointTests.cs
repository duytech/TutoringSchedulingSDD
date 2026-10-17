using BrightPath.Api.IntegrationTests.Infrastructure;
using BrightPath.Application.Tutors;

namespace BrightPath.Api.IntegrationTests;

/// <summary>The rooms, tutors and students over HTTP, as seeded. Read only.</summary>
[Collection(ApiCollection.Name)]
public sealed class ReferenceListEndpointTests(BrightPathApiFactory brightPathApiFactory)
{
    [Fact]
    public async Task Rooms_lists_the_six_rooms_in_order()
    {
        var rooms = await ApiCalls.GetRooms(brightPathApiFactory.CreateClient());

        Assert.Equal(["R1", "R2", "R3", "R4", "R5", "R6"], rooms.Select(r => r.Id));
    }

    [Fact]
    public async Task Tutors_lists_the_seeded_tutors_in_order()
    {
        var tutors = await ApiCalls.GetTutors(brightPathApiFactory.CreateClient());

        Assert.Equal(
            [
                new TutorView("T1", "Ngoc Anh", "Maths"),
                new TutorView("T2", "Pham Duc", "English"),
                new TutorView("T3", "Le Thu", "Physics"),
            ],
            tutors);
    }

    [Fact]
    public async Task Students_lists_the_seeded_students_by_name()
    {
        var students = await ApiCalls.GetStudents(brightPathApiFactory.CreateClient());

        Assert.Equal(
            ["Bui An Nhien", "Do Van Kien", "Le Minh Chau", "Nguyen Thi Ha", "Tran Bao Long", "Vu Ha My"],
            students.Select(student => student.Name));
        Assert.DoesNotContain(students, student => student.Id == Guid.Empty);
    }
}
