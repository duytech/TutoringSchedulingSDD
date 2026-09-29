# Phase 11: Create session — Plan

Tasks in order. The whole phase is one commit at the end.

1. **Shared projections** (`Data/SessionQueries.cs`)
   - `ToRuleSessions(IQueryable<Session>)` and `ToDaySessions(IQueryable<Session>)`: the projections that `ReportEndpoints` and `ScheduleEndpoints` do today. Both endpoints switch to them.

2. **Session view**
   - Make `ScheduleDay`'s per-session view public (`ScheduleDay.View(session, changes, now, policy)`).

3. **Booking check** (`Domain/BookingCheck.cs`)
   - `RuleCodes.InThePast = "in-the-past"` (not in `All`).
   - `BookingCheck.Conflicts(candidate, sameDaySessions, now, policy)`: `in-the-past` first, then `ScheduleRules.Check` over the day plus the candidate, keeping only violations that contain the candidate's id.

4. **Endpoints** (`Endpoints/SessionEndpoints.cs`)
   - `CreateSessionRequest(string? TutorId, string? RoomId, string? StartsAt, int? DurationMin, Guid[]? StudentIds)`.
   - `POST /`:
     1. Validate (`requirements.md` 400 table). Tutor, room and students are looked up in the database. Return `ValidationProblem` with every error.
     2. Begin a transaction. `pg_advisory_xact_lock` on a key built from the tutor id and local date.
     3. Load the day's active sessions (`SessionQueries.ToRuleSessions`), build the candidate, run `BookingCheck`. Conflicts → 409 `ProblemDetails` with `conflicts`.
     4. Insert the session, its attendees and the `created` change. `SaveChanges`, commit.
     5. `DbUpdateException` with `PostgresException` `23P01` → 409 with the conflict for the constraint name.
     6. 201 `Created($"/api/sessions/{id}", view)`.
   - `GET /{id:guid}`: load with `SessionQueries.ToDaySessions` plus its changes, return `ScheduleDay.View`, or 404.
   - OpenAPI metadata: `.WithName`, `.WithSummary`, `.Produces<ScheduleSessionView>(201)`, `.ProducesValidationProblem()`, `.ProducesProblem(409)`, `.ProducesProblem(404)`.
   - Call `MapSessionEndpoints()` from `Program.cs`.

5. **Unit tests** (`BookingCheckTests`)
   - On the real export (`SeedPlanner`, clock at 2026-03-06 10:00): rows 1–3 and 5–12 of the table in `requirements.md`, one test each (rows 1, 2, 3 and 12 have no conflicts).
   - Row 4 as a hand-made case: the candidate against a copy of itself.
   - Edges: a start equal to now is fine. One minute before now is `in-the-past`. T1's existing load break on 03-06 is not reported for a T2 booking that day. A cancelled session in the same room and time does not block.
   - Message text: all dates and times are local and do not depend on the machine's culture.

6. **`.http` file**
   - Variables for two student ids (to paste from `/api/schedule`). One valid create, the three-overlap 409, the load 409, a 400, and `GET /api/sessions/{id}`.

7. **Build and test**
   - `dotnet build` (warnings as errors). `dotnet test`.

8. **Run it**
   - Reseed the dev DB: `DROP DATABASE brightpath WITH (FORCE)` with `psql` (not on PATH, see the local Postgres setup), then `dotnet run` recreates, migrates and seeds it. Checks from `validation.md` with `curl`.
   - Drop and reseed again at the end, so the dev DB is back to the export.

9. **Docs in the same commit**
   - `DECISIONS.md` §3: the API rows, the "in the past" rule row, `changed_by` = `centre` on a `created` change.
   - `specs/roadmap.md` phase 11: `GET /api/sessions/{id}` and `in-the-past`.

10. **Commit**
    - `feat: book a session, refusing every rule it breaks with one 409`. The body says why: this is the write where a double-booking gets in, the 409 lists every conflict at once so the receptionist can fix the booking in one go, the code rules are the report's own rules, and the constraints plus the advisory lock hold when two people book at the same moment.
