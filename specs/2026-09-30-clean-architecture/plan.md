# Phase 20: Clean Architecture — Plan

The tasks run in order, and the whole phase is one commit at the end. After each task, the solution builds with zero warnings.

1. **Projects**
   - Create `src/BrightPath.Domain`, `src/BrightPath.Application` and `src/BrightPath.Infrastructure` as `net10.0` class libraries. `Directory.Build.props` applies to them.
   - References: Application → Domain. Infrastructure → Application. Api → Application and Infrastructure.
   - Packages: EF Core, Npgsql, `EFCore.NamingConventions`, `CsvHelper` and `HealthChecks.EntityFrameworkCore` move from Api to Infrastructure. Api keeps `Microsoft.EntityFrameworkCore.Design`, because it is the startup project for `dotnet ef`, along with OpenAPI and Scalar.
   - Add the three projects to `BrightPath.slnx` under `/src/`.

2. **Domain**
   - Move the entities, `Values`, `BookingPolicy`, `ScheduleRules`, `BookingCheck`, `CancelCheck` and `MoveCheck`. The namespace becomes `BrightPath.Domain`.
   - Cut each `*Configuration` class out of the entity files, and `SqlList` out of `Values.cs`. They go to Infrastructure in task 3.
   - The Domain project has no package reference.

3. **Infrastructure**
   - `Data/` → `Persistence/`: `BrightPathDbContext`, `SessionQueries`, `Configurations/`, `SqlList`.
   - `Migrations/` → `Persistence/Migrations/`. Change the namespace, then replace `BrightPath.Api.Domain.` with `BrightPath.Domain.` in the Designer and snapshot strings.
   - `Seed/` (with the two CSV files, `CopyToOutputDirectory="PreserveNewest"`), plus `Time/FixedTimeProvider`.
   - Port implementations: `EfUnitOfWork`, `PostgresBookingLocks` (+ `BookingLocks`), `SessionRepository`, `ReferenceData`, `ScheduleReader`.
   - `AddInfrastructure(connectionString)` registers the DbContext (Npgsql + snake case) and the ports, scoped. `InitialiseDatabaseAsync(seed)` runs the migrations and the seed, as `Program.cs` does today.

4. **Application**
   - `Common/Result<T>` and its errors.
   - `Abstractions/`: the ports, `SlotTakenException` and `SlotKind`.
   - Move `ScheduleDay` and `TutorDaySheet`, together with their records and views.
   - The handlers, extracted from the endpoints in the same step order: `GetSchedule`, `GetTutorDay`, `GetViolations`, `GetSession`, `CreateSession`, `CancelAttendee`, `MoveSession`. `SessionInput` holds the shared validation. `RaceConflict` builds the violation from a `SlotKind`.
   - `AddApplication()` registers the handlers, scoped.

5. **Api**
   - `Http/ResultHttp.cs`: `Validation` → `ValidationProblem`. `NotFound` and `BadRequest` → `Problem` with the same title, detail and status as today. `Conflict` → today's 409 with `conflicts`.
   - Each endpoint injects its handler, calls it, and maps the result. The OpenAPI metadata stays unchanged.
   - `Program.cs` calls `AddApplication()` and `AddInfrastructure(...)`. The policy, the clock, ProblemDetails, the binding status-code selector, OpenAPI and the health checks stay as before. Then it runs `InitialiseDatabaseAsync`.
   - Delete the emptied `Data/`, `Domain/` and `Seed/` folders from Api.

6. **Tests**
   - `BrightPath.Api.UnitTests` references Domain, Application and Infrastructure instead of Api. `BrightPath.Api.IntegrationTests` keeps its reference to Api. Only the `using` lines change.
   - New `LayerTests`: the Domain assembly references no `Microsoft.EntityFrameworkCore*`, `Npgsql*` or `Microsoft.AspNetCore*`. The Application assembly references no EF Core, no Npgsql and no `BrightPath.Infrastructure`. The test uses `Assembly.GetReferencedAssemblies()`.
   - New `CreateSessionHandlerTests` with hand-written fakes of the ports, no database:
     - A body with no offset on `startsAt` and a `durationMin` of 45 → `Validation` naming both fields.
     - A fake unit of work whose `SaveChangesAsync` throws `SlotTakenException(SlotKind.Room)` → `Conflict` with one `room-overlap`, and no commit.
     - A free slot → the value, and the fake repository holds one session and one `created` change.
   - The fakes are one class, `Fakes/InMemoryBooking`, that implements every port in memory.

7. **Build and test**
   - `dotnet build BrightPath.slnx` (zero warnings). `dotnet test tests/BrightPath.Api.UnitTests`, then `dotnet test tests/BrightPath.Api.IntegrationTests`.
   - `dotnet ef migrations list --project src/BrightPath.Infrastructure --startup-project src/BrightPath.Api`.

8. **Run it**
   - Fresh dev DB, then `dotnet run --project src/BrightPath.Api`. It seeds, and `/health` is healthy.
   - Run create, cancel and move from `BrightPath.Api.http`, and compare the JSON with a run on `main`.
   - With the **Playwright CLI**, check that the Today view still renders 03-06.
   - Reseed the dev DB at the end.

9. **Docs in the same commit** (`requirements.md` "Docs")

10. **Commit** (only once the author asks)
    - `refactor: split the API into Domain, Application, Infrastructure and Api projects`. The body says why: the endpoints had grown to 500 lines mixing HTTP, SQL locks and rules, and a use case could only be tested through HTTP. The domain is now free of EF, and each use case is a handler behind small ports. No behaviour changed, and the integration tests pass unchanged.
