# Phase 6: Schema, core tables — Validation

The phase can be merged when every item below is true.

## Automated

- [ ] `dotnet build` succeeds with zero warnings (warnings are errors).
- [ ] `dotnet test` passes:
  - `HealthEndpointTests` still passes.
  - The new `SchemaTests` test proves `HasPendingModelChanges()` is false, so the migration matches the model.
- [ ] No test writes rows to the dev database.

## Migration applies

- [ ] Drop the dev DB (`dropdb brightpath`), then `dotnet run --project src/BrightPath.Api`. The app starts, creates the DB and applies `InitialSchema` with no error.
- [ ] `GET /health` returns `Healthy`.
- [ ] Running the app a second time applies nothing and still starts.

## Schema is what the requirements say (psql)

- [ ] `\dt` lists `tutors`, `rooms`, `students`, `sessions`, `attendees`, `booking_changes` (plus `__EFMigrationsHistory`). All names are snake_case.
- [ ] `SELECT id FROM rooms ORDER BY id;` returns `R1`…`R6`.
- [ ] `\d sessions` shows `starts_at`/`ends_at` as `timestamp with time zone`, no `slot` column, and `ck_sessions_duration`.
- [ ] `\d attendees` shows the three CHECKs, the unique `source_lesson_id` and the unique `(session_id, student_id)`.
- [ ] Every foreign key is `ON DELETE RESTRICT`.

## CHECKs refuse bad data (psql, inside a transaction that is rolled back)

Run in `BEGIN; … ROLLBACK;` so the dev DB stays empty for the phase 8 seed.

- [ ] A session of 45 minutes is refused by `ck_sessions_duration`. 60 and 90 are accepted.
- [ ] An attendee with `status = 'maybe'` is refused by `ck_attendees_status`.
- [ ] An attendee with `status = 'cancelled'` and `cancelled_at` null is refused by `ck_attendees_cancelled_consistent`.
- [ ] An attendee with `cancelled_by = 'mai'` is refused by `ck_attendees_cancelled_by`.
- [ ] A booking change with `kind = 'edited'` is refused by `ck_booking_changes_kind`.
- [ ] Two students with the same name are refused by the unique index.
- [ ] Deleting a room that a session uses is refused (FK restrict).

## Docs and commit

- [ ] `DECISIONS.md` §3 matches the migration (uuid keys, `source_lesson_id`, `note`).
- [ ] `specs/roadmap.md` phase 7 says `tstzrange`.
- [ ] `dotnet-tools.json` (repo root, the .NET 10 default location) is committed and `dotnet tool restore` works on a clean clone.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
