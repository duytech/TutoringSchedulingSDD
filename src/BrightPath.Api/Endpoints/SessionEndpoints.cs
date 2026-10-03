using BrightPath.Api.Http;
using BrightPath.Application.Sessions;

namespace BrightPath.Api.Endpoints;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var sessions = app.MapGroup("/api/sessions").WithTags("Sessions");

        sessions.MapGet("/", GetDaySessions)
            .WithName("GetDaySessions")
            .WithSummary("One day's sessions")
            .WithDescription(
                "Every session whose local start date is the given date (default: today on the pinned clock), " +
                "cancelled ones included, with attendees and changes, plus the clock's now. " +
                "The rooms and tutors come from /api/rooms and /api/tutors.")
            .Produces<DaySessionsView>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        sessions.MapPost("/", CreateSession)
            .WithName("CreateSession")
            .WithSummary("Book a session, or refuse it with every rule it breaks")
            .WithDescription(
                "startsAt is a local time with its offset (2026-03-07T13:00:00+07:00). durationMin is 60 or 90. " +
                "studentIds are 1 or 2 students (ids from GET /api/sessions). A 409 lists every conflict at once: " +
                "in-the-past, room-overlap, tutor-overlap, student-overlap, tutor-load, closed-day, outside-hours, " +
                "too-many-attendees.")
            .Produces<SessionView>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        sessions.MapGet("/{id:guid}", GetSession)
            .WithName("GetSession")
            .WithSummary("One session, in the same shape as an item of GET /api/sessions")
            .Produces<SessionView>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        sessions.MapPost("/{id:guid}/attendees/{attendeeId:guid}/cancel", Cancel)
            .WithName("CancelAttendee")
            .WithSummary("Cancel one student's place, freeing the slot")
            .WithDescription(
                "cancelledBy is family, tutor or centre. Only a family cancel less than 4 hours before the start is " +
                "chargeable. The change is flagged afterCutoff after 16:00 the day before. When no one else is " +
                "booked, the session is cancelled too. A 409 lists already-started and already-cancelled.")
            .Produces<SessionView>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        sessions.MapPost("/{id:guid}/move", Move)
            .WithName("MoveSession")
            .WithSummary("Move a session to a new time, room or length, linking the old one to the new one")
            .WithDescription(
                "In one transaction the old session is cancelled, the new one is created, and the old one points at " +
                "the new one (movedTo). Both get a 'moved' change. The new slot passes the same rules as a new " +
                "booking, with the old session left out. A move is never chargeable. A 409 lists already-started, " +
                "already-cancelled and the create conflicts.")
            .Produces<SessionView>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static async Task<IResult> GetDaySessions(DateOnly? date, GetDaySessionsHandler handler, CancellationToken ct) =>
        TypedResults.Ok(await handler.HandleAsync(date, ct));

    private static async Task<IResult> CreateSession(
        CreateSessionRequest request, CreateSessionHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(request, ct))
            .ToHttp(view => TypedResults.Created($"/api/sessions/{view.Id}", view));

    private static async Task<IResult> GetSession(Guid id, GetSessionHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(id, ct)).ToHttp(view => TypedResults.Ok(view));

    private static async Task<IResult> Cancel(
        Guid id, Guid attendeeId, CancelAttendeeRequest request, CancelAttendeeHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(id, attendeeId, request, ct)).ToHttp(view => TypedResults.Ok(view));

    private static async Task<IResult> Move(
        Guid id, MoveSessionRequest request, MoveSessionHandler handler, CancellationToken ct) =>
        (await handler.HandleAsync(id, request, ct))
            .ToHttp(view => TypedResults.Created($"/api/sessions/{view.Id}", view));
}
