# Phase 12: Integration tests: conflicts — Requirements

Roadmap: Stage C, phase 12 (est. 12 min, now 17 with the two race tests). One atomic commit.

## Goal

Prove, through HTTP and against a real Postgres, that `POST /api/sessions` refuses each kind of conflict and accepts a valid exam pair. Also prove that the two race guards from phase 11 actually hold: the exclusion constraint and the tutor-day advisory lock. The unit tests (`BookingCheckTests`, `ScheduleDayTests`) prove the rules. These tests prove the wiring: binding, the transaction, the lock, the constraint mapping, the clock from config, and the JSON shape.

`dotnet test` must never touch the dev database.

## In scope

- A test host (`BrightPathApiFactory`) that runs the API on a throwaway database `brightpath_test_<guid>` on the local Postgres. Startup migrates it and seeds the export. The database is dropped when the run ends.
- One test per rule from the roadmap: student, room, tutor, tutor-load, Monday, plus a valid exam pair.
- Two race tests: the constraint path and the advisory lock.
- `GET /api/schedule` on the pinned day, and again with `Clock:Now` moved.
- A guard test: the test host is on a `brightpath_test_` database, not `brightpath`.
- `HealthEndpointTests` moves onto the same host. Today it uses a plain `WebApplicationFactory<Program>`, so it migrates and seeds the **dev** database.

## Out of scope

- 400 input cases, `GET /api/sessions/{id}` 404, and the violation report over HTTP. They are simple and already checked by hand in phases 9 and 11.
- Cancel (phase 13 brings its own tests).
- Testcontainers or Docker (`specs/tech-stack.md`: not used).
- Cleaning up test databases left behind by a crashed run. The README (phase 14) gives the command.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Test database | Local Postgres, `CREATE DATABASE brightpath_test_<guid>` (done by `MigrateAsync` at startup), then `DROP DATABASE … WITH (FORCE)` at the end of the run. | Matches the roadmap and tech stack. No Docker. |
| Race tests | Two tests: one for the exclusion constraint, one for the advisory lock. | Phase 11 left the race to phase 12. It is the guarantee the brief cares about most. |
| How a race is forced | The test holds its own open transaction on the test database, and one API request runs into it. It does **not** fire two parallel HTTP requests. | Two parallel requests usually run one after the other, so the test would also pass with no lock and no constraint mapping. A held transaction makes the interleaving certain, so each test fails if its guard is removed. |
| Isolation | One database per run, shared by every API test (xUnit collection fixture, so the tests run one after another). Each test books on **its own local date**, so tests cannot see each other's bookings. | Migrating and seeding once keeps the run short. Using a separate date for each test is the simplest way to stay independent. |

## The test host

- `BrightPathApiFactory : WebApplicationFactory<Program>, IAsyncLifetime`, used as an xUnit collection fixture (`[Collection("api")]`).
- The base connection string is read the same way the app reads it: `appsettings.Development.json` of the API, then user-secrets (`UserSecretsId` of the API), then environment variables. The real password lives in user-secrets on this machine. The database name is replaced with `brightpath_test_<guid N>`.
- The new connection string is passed with `UseSetting("ConnectionStrings:BrightPath", …)`, because `Program.cs` reads it before `Build()`.
- `Seed:Enabled` stays true: the tests use the export and its student ids.
- `Clock:Now` stays at the pinned 2026-03-06 10:00 +07:00. The moved-clock test uses `WithWebHostBuilder(b => b.UseSetting("Clock:Now", …))` on the same database. Startup runs again, but migrate is a no-op and the seed skips a database that is not empty.
- `DisposeAsync`: stop the host, `NpgsqlConnection.ClearAllPools()`, then drop the database from the `postgres` maintenance database.
- Helpers:
  - `StudentId(name)`: looked up from `Students` through a scope of `Services`.
  - `OpenDb()`: a `BrightPathDbContext` on the test database, for the race tests.

## Tests

The clock is at 2026-03-06 10:00 +07:00 unless a test says otherwise. "Only X" means the 409 `conflicts` list has exactly one item, with rule X.

| # | Test | Date | Steps | Expected |
|---|---|---|---|---|
| 1 | Student overlap | 03-11 (Wed) | Book T1, R1, 09:00, 60, [Le Minh Chau]. Then T2, R2, 09:00, 60, [Le Minh Chau] | 201, then 409 only `student-overlap` naming Le Minh Chau |
| 2 | Room overlap | 03-12 (Thu) | Book T1, R1, 09:00, 60, [Le Minh Chau]. Then T2, R1, 09:30, 60, [Tran Bao Long] | 201, then 409 only `room-overlap` |
| 3 | Tutor overlap | 03-13 (Fri) | Book T1, R1, 09:00, 60, [Le Minh Chau]. Then T1, R2, 09:00, 60, [Tran Bao Long] | 201, then 409 only `tutor-overlap` |
| 4 | Tutor load | 03-06 (seed) | T1, R4, 14:30, 60, [Do Van Kien] | 409 only `tutor-load`, with 8 `sessionIds` (the 7 seeded plus the new one) |
| 5 | Closed on Monday | 03-16 (Mon) | T2, R2, 10:00, 60, [Le Minh Chau] | 409 only `closed-day` |
| 6 | Valid exam pair | 03-18 (Wed) | T3, R5, 14:00, 90, [Nguyen Thi Ha, Do Van Kien] | 201. `Location` is `/api/sessions/{id}`, and a `GET` of it returns 200 with 2 `booked` attendees and one `created` change (`changedBy` `centre`, `afterCutoff` false). `GET /api/schedule?date=2026-03-18` lists it under R5 and T3 |
| 7 | Race: exclusion constraint | 03-19 (Thu) | See below | 409 only `room-overlap`, message `R6 was booked by someone else at the same moment.`, `sessionIds` only the new id, `lessonIds` empty. Afterwards R6 on 03-19 holds only the test's session |
| 8 | Race: tutor-day lock | 03-17 (Tue) | See below | 409 only `tutor-load` |
| 9 | Schedule on the pinned day | 03-06 | `GET /api/schedule` | `date` 2026-03-06, `now` `2026-03-06T10:00:00+07:00`, 10 sessions. R1 has 7, R4–R6 are empty. L018 and L019 `past`, the rest `upcoming` |
| 10 | Schedule with the clock moved | 03-07 | Host with `Clock:Now` = `2026-03-07T10:00:00+07:00`. `GET /api/schedule` | `date` 2026-03-07. L028 (09:00–10:30) `in-progress`, L029 and L030 `upcoming` |
| 11 | Never the dev database | – | Read the database name from the host's `BrightPathDbContext` | Starts with `brightpath_test_` |
| 12 | Health | – | `GET /health` (moved test) | `Healthy` |

No test books on 03-06 or 03-07 successfully, so tests 9 and 10 see only the export.

### Test 7: the exclusion constraint under a race

1. The test opens its own transaction on the test database and inserts a session T1, R6, 03-19 10:00–11:00 (no attendees). It does not commit.
2. It starts `POST /api/sessions` for T2, R6, 03-19 10:00, 60, [Tran Bao Long], without awaiting it. The API takes the lock for T2, not T1, so it is not blocked there. It reads the day, but the test's session is not committed, so the API does not see it and the code checks pass. The API's insert then waits on `ex_sessions_room_slot`.
3. The test waits (at most 10 s) until another backend on the test database is waiting on a lock (`pg_stat_activity.wait_event_type = 'Lock'`). If the POST finishes first, the test fails with "the insert did not wait on the constraint".
4. The test commits. The API's insert fails with `23P01` and the response is the race 409.

Without the `23P01` mapping, this is a 500, so the test fails.

### Test 8: the advisory lock under a race

1. Book 5 sessions for T2 on 03-17 through the API: R1 at 09:00, 10:30, 12:00, 13:30 and 15:00, 60 min, [Vu Ha My]. All 201.
2. The test opens its own transaction, takes the same advisory lock the API takes for T2 on 2026-03-17, and inserts a 6th T2 session (R2, 16:30–17:30). It does not commit.
3. It starts `POST /api/sessions` for T2, R3, 03-17 18:00, 60, [Vu Ha My], without awaiting it.
4. The test waits (at most 10 s) until another backend is waiting on an advisory lock (`wait_event = 'advisory'`). If the POST finishes first, the test fails with "the booking did not wait for the tutor-day lock".
5. The test commits. The API then reads 6 sessions and refuses the 7th.

Without the lock, the API reads only 5 committed sessions and returns 201, so the test fails.

The lock key comes from one public helper in the API (`BookingLocks.TutorDay(tutorId, date)`), so the test and the endpoint cannot build different keys.

## Context

- Phase 11 spec (`specs/2026-09-29-create-session/`): the 409 shape, the race conflicts, the advisory lock.
- Phase 10 spec: `Clock:Now`, the schedule shape and `state`.
- `specs/tech-stack.md`: Tests row, database permissions (`CREATE DATABASE`, `btree_gist`).
