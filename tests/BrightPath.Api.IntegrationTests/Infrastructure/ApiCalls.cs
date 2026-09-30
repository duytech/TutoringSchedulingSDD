using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using BrightPath.Application.Rooms;
using BrightPath.Application.Schedule;
using BrightPath.Application.Tutors;
using BrightPath.Domain;
using Npgsql;

namespace BrightPath.Api.IntegrationTests.Infrastructure;

public sealed record Conflict(string Rule, IReadOnlyList<Guid> SessionIds, IReadOnlyList<string> LessonIds, string Message);

/// <summary>Small helpers for calling the API the way a receptionist's client would.</summary>
public static class ApiCalls
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary><paramref name="localStart"/> is <c>yyyy-MM-dd HH:mm</c> in the centre's time (+07:00).</summary>
    public static Task<HttpResponseMessage> Book(
        HttpClient client, string tutorId, string roomId, string localStart, int durationMin, params Guid[] studentIds) =>
        client.PostAsJsonAsync("/api/sessions", new
        {
            tutorId,
            roomId,
            startsAt = $"{localStart.Replace(' ', 'T')}:00+07:00",
            durationMin,
            studentIds,
        });

    public static Task<HttpResponseMessage> Cancel(
        HttpClient client, Guid sessionId, Guid attendeeId, string cancelledBy, string? note = null) =>
        client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/attendees/{attendeeId}/cancel", new { cancelledBy, note });

    /// <summary><paramref name="localStart"/> is <c>yyyy-MM-dd HH:mm</c> in the centre's time (+07:00).</summary>
    public static Task<HttpResponseMessage> Move(
        HttpClient client, Guid sessionId, string localStart, string movedBy, string? roomId = null) =>
        client.PostAsJsonAsync(
            $"/api/sessions/{sessionId}/move",
            new { startsAt = $"{localStart.Replace(' ', 'T')}:00+07:00", roomId, movedBy });

    public static async Task<ScheduleSessionView> ReadSession(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ScheduleSessionView>(Json))!;

    public static async Task<ScheduleDayView> Schedule(HttpClient client, string? date = null) =>
        (await client.GetFromJsonAsync<ScheduleDayView>(
            date is null ? "/api/schedule" : $"/api/schedule?date={date}", Json))!;

    public static async Task<IReadOnlyList<RoomView>> Rooms(HttpClient client) =>
        (await client.GetFromJsonAsync<List<RoomView>>("/api/rooms", Json))!;

    public static async Task<IReadOnlyList<TutorView>> Tutors(HttpClient client) =>
        (await client.GetFromJsonAsync<List<TutorView>>("/api/tutors", Json))!;

    public static Task<HttpResponseMessage> GetTutorDay(HttpClient client, string tutorId, string? date = null) =>
        client.GetAsync(date is null ? $"/api/tutors/{tutorId}/day" : $"/api/tutors/{tutorId}/day?date={date}");

    public static async Task<TutorDaySheetView> TutorDay(HttpClient client, string tutorId, string? date = null)
    {
        var response = await GetTutorDay(client, tutorId, date);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TutorDaySheetView>(Json))!;
    }

    public static async Task<IReadOnlyList<Conflict>> ReadConflicts(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("conflicts").Deserialize<List<Conflict>>(Json)!;
    }

    /// <summary>
    /// Waits until another backend on the test database is blocked on a lock that <paramref name="waitEvent"/>
    /// accepts. Fails if <paramref name="request"/> finishes first: then it never waited, so the guard is missing.
    /// </summary>
    public static async Task WaitUntilBlocked(
        string connectionString, Func<string, string, bool> waitEvent, Task request, string failure)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        var timer = Stopwatch.StartNew();

        while (timer.Elapsed < TimeSpan.FromSeconds(10))
        {
            Assert.False(request.IsCompleted, failure);
            if (await AnyBlocked(connection, waitEvent))
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail($"{failure} (nothing was blocked after 10 s)");
    }

    private static async Task<bool> AnyBlocked(NpgsqlConnection connection, Func<string, string, bool> waitEvent)
    {
        await using var query = new NpgsqlCommand(
            """
            SELECT wait_event_type, wait_event FROM pg_stat_activity
            WHERE datname = current_database() AND pid <> pg_backend_pid() AND wait_event_type IS NOT NULL
            """,
            connection);
        await using var reader = await query.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (waitEvent(reader.GetString(0), reader.GetString(1)))
            {
                return true;
            }
        }

        return false;
    }
}
