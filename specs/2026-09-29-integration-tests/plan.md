# Phase 12: Integration tests: conflicts — Plan

Tasks in order. The whole phase is one commit at the end.

1. **Lock key helper** (API)
   - `Domain/BookingLocks.cs`: `public static string TutorDay(string tutorId, DateOnly date)` returns `tutor-day:{tutorId}:{yyyy-MM-dd}` (invariant culture).
   - `SessionEndpoints.CreateSession` uses it. No behaviour change.

2. **Test host** (`tests/BrightPath.Api.Tests/Infrastructure/BrightPathApiFactory.cs`)
   - Base connection string: `new ConfigurationBuilder()` with the API's `appsettings.Development.json` (optional), `AddUserSecrets<Program>(optional: true)` and `AddEnvironmentVariables()`, then `GetConnectionString("BrightPath")`. Fail with a clear message if it is missing.
   - `NpgsqlConnectionStringBuilder`: `Database = $"brightpath_test_{Guid.NewGuid():N}"`.
   - `ConfigureWebHost`: `UseEnvironment("Development")`, `UseSetting("ConnectionStrings:BrightPath", …)`.
   - `IAsyncLifetime.InitializeAsync`: touch `Services` so startup (migrate + seed) runs once, before the first test.
   - `DisposeAsync`: `base.DisposeAsync()`, `NpgsqlConnection.ClearAllPools()`, then on `Database = postgres`: `DROP DATABASE IF EXISTS "<name>" WITH (FORCE)`.
   - Helpers: `StudentId(name)`, `OpenDb()`, `ConnectionString`.
   - `ApiCollection.cs`: `[CollectionDefinition("api")] : ICollectionFixture<BrightPathApiFactory>`.

3. **Test helpers** (`Infrastructure/ApiCalls.cs`, static methods)
   - `Book(client, tutor, room, localStart, duration, params studentIds)`: posts the request with `startsAt` as `yyyy-MM-ddTHH:mm:ss+07:00`.
   - `ReadConflicts(response)`: reads `ProblemDetails.conflicts` into a list of `(rule, sessionIds, lessonIds, message)`.
   - `WaitUntilBlocked(connectionString, waitEvent predicate, Task post, failure)`: polls `pg_stat_activity` on the test database (`pid <> pg_backend_pid()`) every 50 ms. Returns when a backend is waiting, and fails if `post` completes first or the timeout passes.
   - Read JSON with `System.Text.Json` (`JsonDocument` or small records), with web defaults.

4. **Conflict tests** (`SessionConflictTests`, `[Collection("api")]`)
   - Tests 1–6 from `requirements.md`, one `[Fact]` each, each on its own date.

5. **Race tests** (`BookingRaceTests`, `[Collection("api")]`)
   - Test 7: `OpenDb()`, `BeginTransactionAsync`, add a `Session` (T1, R6, 03-19 10:00, 60 min, id `Guid.CreateVersion7()`), `SaveChangesAsync`. Start the POST. `WaitUntilBlocked` on `wait_event_type = 'Lock'`. Commit. Assert the race 409. Then `GET /api/schedule?date=2026-03-19`: R6 lists one session, and it is the test's id.
   - Test 8: 5 bookings through the API. `OpenDb()`, transaction, `SELECT pg_advisory_xact_lock(hashtextextended({BookingLocks.TutorDay("T2", 2026-03-17)}, 0))`, add the 6th session, `SaveChangesAsync`. Start the POST. `WaitUntilBlocked` on `wait_event = 'advisory'`. Commit. Assert only `tutor-load`.
   - Each test disposes its context in `finally`, so a failed test cannot keep a transaction open and hang the run.

6. **Schedule tests** (`ScheduleEndpointTests`, `[Collection("api")]`)
   - Test 9 on the shared host.
   - Test 10: `using var moved = factory.WithWebHostBuilder(b => b.UseSetting("Clock:Now", "2026-03-07T10:00:00+07:00"))`.

7. **Host tests**
   - `HealthEndpointTests` moves to `[Collection("api")]` with `BrightPathApiFactory`.
   - Test 11 (`TestHostTests` or in the same class): the database name from `GetDbConnection().Database` starts with `brightpath_test_`.

8. **Build and test**
   - `dotnet build` (warnings as errors). `dotnet test`, twice in a row. Both runs pass.
   - After the runs, `psql -l` (see the local Postgres setup) shows no `brightpath_test_%` database left.
   - Check that the dev database is untouched: its sessions count is the same as before the run.

9. **Break it on purpose** (not committed)
   - Comment out the advisory lock line → test 8 fails.
   - Remove the `23P01` catch → test 7 fails (500).
   - Restore both.

10. **Docs in the same commit**
    - `specs/roadmap.md` phase 12: the two race tests and the health test move, est. 17.
    - `specs/tech-stack.md` Tests row: one database per run, shared by the tests, each test on its own date. Races are forced with a held transaction.

11. **Commit**
    - `test: prove conflicts and races through the API on a throwaway database`. The body says why: the unit tests prove the rules, and these prove the wiring (binding, transaction, lock, constraint mapping, clock from config). The race tests hold a transaction open instead of firing parallel requests, so they fail when a guard is removed. `dotnet test` no longer touches the dev database.
