using BrightPath.Application.Rooms;

namespace BrightPath.Api.Endpoints;

public static class RoomEndpoints
{
    public static IEndpointRouteBuilder MapRoomEndpoints(this IEndpointRouteBuilder app)
    {
        var rooms = app.MapGroup("/api/rooms").WithTags("Rooms");

        rooms.MapGet("/", GetRooms)
            .WithName("GetRooms")
            .WithSummary("Every room")
            .WithDescription("Every room of the centre, ordered by id. Reference data: the same on every day.")
            .Produces<IReadOnlyList<RoomView>>();

        return app;
    }

    private static async Task<IResult> GetRooms(GetRoomsHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(ct));
}
