# Phase 20: Clean Architecture — Requirements

Roadmap: not on the roadmap. This is a refactor that comes after phase 19. It is one atomic commit.

## Goal

Split the backend into four projects, with every dependency pointing inward:

```
BrightPath.Api  →  BrightPath.Infrastructure  →  BrightPath.Application  →  BrightPath.Domain
```

Today, everything lives in `src/BrightPath.Api`, which causes two problems:

- `Endpoints/SessionEndpoints.cs` has grown to 509 lines. Each handler mixes HTTP binding, input validation, EF Core queries, raw SQL locks (`FOR UPDATE`, `pg_advisory_xact_lock`), catching `PostgresException`, and the orchestration of the use case.
- The entities in `Domain/` also hold their EF `IEntityTypeConfiguration` classes, so the domain depends on EF Core. A use case (create, cancel, move) can only be tested through HTTP against a real Postgres.

After the split, the domain has no framework dependency. Each use case is a small handler that can be unit tested with fakes. The endpoints only translate HTTP to a handler call and back.

This is a **pure refactor**. The behaviour does not change.

## In scope

- Three new class-library projects: `BrightPath.Domain`, `BrightPath.Application`, `BrightPath.Infrastructure`. `BrightPath.Api` stays as the host and the composition root.
- Moving each existing file to its layer, and changing its namespace.
- Extracting one handler per use case from the endpoints.
- Small ports (interfaces) in Application, implemented in Infrastructure.
- Moving the EF configurations, the migrations and the seed loader to Infrastructure.
- New unit tests: the layer rules, and one handler tested with fakes.
- Docs in the same commit.

## Out of scope

- Any change to routes, request or response JSON, status codes, ProblemDetails titles or details, or the OpenAPI operation names.
- Any change to the DB schema. No new migration.
- Any change to the rules, the locks, the transactions or the race handling.
- Renaming the test projects (`BrightPath.Api.UnitTests`, `BrightPath.Api.IntegrationTests`).
- The web project.
- A rich domain model (behaviour moved onto the entities). The pure checks (`BookingCheck`, `CancelCheck`, `MoveCheck`, `ScheduleRules`) stay as they are.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Structure | 4 projects, not folders inside one project. | The compiler enforces the direction of the dependencies. Folders would only be a convention. |
| Data access | Application defines small repository ports plus a unit of work. Application references neither EF Core nor Npgsql. | The locks, the raw SQL and the `23P01` handling are Postgres details. With them behind ports, a handler can be tested with fakes. |
| Calling a use case | One plain handler class per use case (`CreateSessionHandler.HandleAsync`). The handlers are registered in DI and injected into the endpoint. | No MediatR: its licence is now commercial, and it adds a layer of indirection for 7 use cases. |
| Handler result | `Result<T>`: a value or one error (`Validation`, `NotFound`, `BadRequest`, `Conflict`). Api maps it to `IResult`. | Application must not know about HTTP. Each error kind maps to exactly one of today's responses. |
| Names | The request records keep their names (`CreateSessionRequest` and so on). | The OpenAPI schema names stay the same. |

`specs/tech-stack.md` lists "Clean Architecture layers" under "Not used" ("too heavy for a single feature built in 2.5h"). That was true at phase 5. It no longer is: there are now 7 use cases, and the use cases can only be tested through HTTP. The doc is updated with this reason.

## Target layout

```
src/
  BrightPath.Domain/            no package references
    Session, Attendee, BookingChange, Room, Tutor, Student      (EF configuration classes removed)
    Values.cs                   AttendeeStatus, CancelledBy, ChangeKind (SqlList moves out)
    BookingPolicy.cs            BookingPolicy, BookingPolicyOptions
    ScheduleRules.cs            RuleSession, RuleAttendee, ScheduleViolation, RuleCodes, ScheduleRules
    BookingCheck.cs, CancelCheck.cs, MoveCheck.cs

  BrightPath.Application/       → Domain. Only Microsoft.Extensions.DependencyInjection.Abstractions
    Abstractions/               IUnitOfWork, ITransaction, IBookingLocks, ISessionRepository,
                                IReferenceData, IScheduleReader, SlotTakenException, SlotKind
    Common/                     Result<T> and its errors
    Schedule/                   DaySession, DayAttendee, the views (ScheduleDayView, ScheduleSessionView, …),
                                ScheduleDay, GetScheduleHandler
    Tutors/                     TutorDaySheet and its views, GetTutorDayHandler
    Reports/                    ViolationReport, GetViolationsHandler
    Sessions/                   SessionInput (durations, note length, the offset parse), RaceConflict,
                                CreateSessionHandler (+ CreateSessionRequest),
                                CancelAttendeeHandler (+ CancelAttendeeRequest),
                                MoveSessionHandler (+ MoveSessionRequest),
                                GetSessionHandler (also builds the response of the 3 write handlers)
    DependencyInjection.cs      AddApplication()

  BrightPath.Infrastructure/    → Application. EF Core, Npgsql, EFCore.NamingConventions, CsvHelper,
                                HealthChecks.EntityFrameworkCore
    Persistence/                BrightPathDbContext, Configurations/*Configuration.cs, SqlList,
                                SessionQueries, Migrations/, EfUnitOfWork, SessionRepository,
                                ReferenceData, ScheduleReader, PostgresBookingLocks, BookingLocks
    Seed/                       SeedCsv, SeedPlanner, SeedLoader, tutors.csv, lessons_export.csv
    Time/                       FixedTimeProvider
    DependencyInjection.cs      AddInfrastructure(connectionString), InitialiseDatabaseAsync(seed)

  BrightPath.Api/               → Application, Infrastructure. Web SDK, OpenAPI, Scalar,
                                EF Core Design (the startup project for `dotnet ef`)
    Program.cs                  composition only
    Endpoints/                  thin: bind → handler → ResultHttp
    Http/ResultHttp.cs          Result<T> → IResult
```

## Ports

Each port is small and has one job.

| Port | Members | Implemented by |
|---|---|---|
| `IUnitOfWork` | `BeginTransactionAsync(ct)` returns an `ITransaction` (`IAsyncDisposable`, `CommitAsync`). `SaveChangesAsync(ct)` throws `SlotTakenException(SlotKind)` on an exclusion violation. | `EfUnitOfWork` maps `23P01` and the constraint name (`ex_sessions_room_slot` → `Room`, `ex_sessions_tutor_slot` → `Tutor`, anything else → `Student`). |
| `IBookingLocks` | `TutorDayAsync(tutorId, date, ct)` | `PostgresBookingLocks`: `pg_advisory_xact_lock(hashtextextended(key, 0))`, with the same key as today's `BookingLocks.TutorDay`. |
| `ISessionRepository` | `GetForUpdateAsync(id)` (the row under `FOR UPDATE`, plus its tracked attendees with `Student`), `ActiveOnAsync(date)`, `ActiveBetweenAsync(from, to)`, `Add(Session)`, `AddChanges(params BookingChange[])` | `SessionRepository` |
| `IReferenceData` | `FindTutorAsync`, `RoomExistsAsync`, `FindStudentsAsync(ids)`, `RoomIdsAsync`, `TutorsAsync` | `ReferenceData` |
| `IScheduleReader` | `DaySessionsAsync(date, tutorId?)`, `DaySessionAsync(id)`, `ChangesOfAsync(sessions)`, `MoveTargetsAsync(sessions)` | `ScheduleReader`, which reuses today's `SessionQueries` extensions as they are |

`RaceConflict` (which builds the `ScheduleViolation` from a `SlotKind`) stays in Application, with the same text as today.

## What must not change

- **The order of the steps in each write handler.** Transaction, row lock or advisory lock, read, check, write, and in move the first `SaveChanges` before the insert. `TRANSACTIONS.md` explains why this order matters. A handler moves out of `SessionEndpoints.cs` line for line, with each `db.*` call replaced by a port call.
- **Every HTTP response:** the status code, the ProblemDetails `title` and `detail`, the `errors` keys, the `conflicts` extension, and the `Location` header.
- **The schema:** `SchemaTests` finds no pending model changes after the namespaces move.
- **Startup:** it migrates, then seeds when `Seed:Enabled` is on and the database is empty. The CSV files still end up in `bin/.../Seed/` of the Api.
- **The integration tests:** they pass with no assertion changed. Only the `using` lines may change.

## Docs

- `specs/tech-stack.md`: the repository layout shows the 4 projects. Clean Architecture leaves "Not used", and the reason is added (see Decisions).
- `README.md`: the seed path becomes `src/BrightPath.Infrastructure/Seed/`. A short "Code layout" section lists the 4 projects and gives the `dotnet ef` command with `--project src/BrightPath.Infrastructure --startup-project src/BrightPath.Api`. The test list mentions the handler and layer tests.
- `TRANSACTIONS.md`: the link to `SessionEndpoints.cs` points to the three handlers instead.
- `BrightPath.slnx`: the three new projects go under `/src/`.

## Context

- `specs/tech-stack.md` ("Where each rule is enforced", "Not used").
- `TRANSACTIONS.md` (why the write endpoints open a transaction).
- Phase 11 (create, the tutor-day lock), phase 13 (cancel, the row lock), phase 18 (move, the two `SaveChanges` calls).
