# Phase 8: Seed loader — Requirements

Roadmap: Stage B, phase 8 (est. 12 min). One atomic commit.

## Goal

Load the front-desk export (`lessons_export.csv`, `tutors.csv`) into the schema from phases 6 and 7, **as history**: every row is kept, rows that break a rule are loaded and flagged, nothing is fixed or dropped (`DECISIONS.md` §1, "Where the brief and the data disagree"). After `dotnet run` on an empty database, the week is there and the exclusion constraints are live.

## In scope

- Copy both CSVs into the repo (`src/BrightPath.Api/Seed/`), copied to the build output. The `.assignment/` folder is not committed, so the app cannot read from it.
- Parse them with **CsvHelper**, mapping by column name.
- Turn the rows into tutors, students, sessions, attendees and booking changes (rules below).
- Work out `legacy_violation` **with an algorithm**, not a hard-coded list of lesson IDs.
- Infer `cancelled_by` from the note, and compute `chargeable` and `after_cutoff` with small policy functions that phases 11 and 13 will reuse.
- Run on **startup**, after `MigrateAsync`, only when the `sessions` table is empty, in one transaction. `Seed:Enabled` (default `true`) turns it off, for the phase 12 test database.
- Unit tests for the row-to-entity mapping, run against the real CSV, with no database.

## Out of scope (later phases)

- Reporting the rule breaks (Monday, 7 a day, overlaps) → phase 9. The seed only sets the flags the constraints need.
- The pinned clock → phase 10. The seed does not read the current time.
- Opening hours, Monday and 6-a-day checks → phase 11. They never block the import.
- API-level tests on a throwaway database → phase 12.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| When it runs | On startup, if `sessions` is empty. Config flag `Seed:Enabled`. | The reviewer only runs `dotnet run`. A second start does not duplicate anything. Tests can turn it off. |
| `legacy_violation` | Worked out by an algorithm (below). | It is right for this export and for any other one. The database checks the result: if the flags are wrong, the insert fails on an `EXCLUDE` constraint. |
| `cancelled_by` | Inferred from a keyword in the note: `family`, `tutor` or `centre`. No match → null. | The export has no such column, but the notes say who cancelled ("family cancelled", "tutor sick"), and Q2 depends on it. |
| `chargeable`, `after_cutoff` | Computed by shared policy functions, not hard-coded per row. | The same rule then applies to seeded history and to new cancels (phase 13). |
| CSV parsing | CsvHelper. | Handles quoting and commas in notes. Maps by header name, so column order does not matter. |

## Mapping rules

### Tutors

- One row per line of `tutors.csv`: `tutor_id` → `id`, `tutor_name` → `name`, `subject`.
- `phone` is **not** stored. Nothing needs it, and it is personal data.

### Students

- One student per distinct `student` name (a name is the identity, DECISIONS §1 assumptions). New uuid each.

### Sessions

- Times are local to `Asia/Ho_Chi_Minh` (+07:00). `starts_at` = `date` + `start_time` in that zone. `ends_at` = `starts_at` + `duration_min`.
- Rows with the same **(date, start_time, duration_min, tutor_id, room)** become **one session** with one attendee per row. That turns the exam pair L009 + L010 into one session with two attendees. Rows with the same tutor and time but a different room stay separate sessions (L033 / L034).
- A session is cancelled when **all** its attendees are cancelled. Its `cancelled_at` is the latest attendee `cancelled_at` (DECISIONS §3: the last attendee cancelled cancels the session).
- `moved_to_session_id` is always null. L032's "moved from Sunday" has no source row to link to, so it is kept only as the note.

### Attendees

- One per CSV row. `session_id`, `student_id`, `status` as in the CSV (`booked`, `cancelled`, `no_show`), `cancelled_at` from the CSV, `source_lesson_id` = `lesson_id`, `note` = `note` (empty → null).
- `slot` is not set by code. The phase 7 trigger fills it.
- `cancelled_by`: see the decision above. Only set on cancelled rows.
- `chargeable`: true only when `cancelled_by = family` **and** the cancel is less than 4 hours before `starts_at` (Q2).

### Booking changes

- One `cancelled` change per cancelled attendee: `changed_at` = `cancelled_at`, `changed_by` = `cancelled_by`, `attendee_id` set, `note` = the CSV note.
- `after_cutoff`: true when `changed_at` is after 16:00 local on the calendar day before the lesson (DECISIONS §1, even when that day is a Monday).
- Booked and no-show rows get **no** `created` change: the export does not say when they were made (DECISIONS §3).

### `legacy_violation`

The flag leaves only the **later** row of each overlapping pair out of the constraints. The earlier row stays covered and keeps guarding the slot.

1. Order sessions by `starts_at`, then by their smallest `lesson_id`. "Later" means later in this order, so a tie on time is broken by lesson ID.
2. **Session flag:** a session that is not cancelled gets `legacy_violation = true` when it overlaps (half-open `[)`) an earlier, not cancelled, not flagged session with the **same room or the same tutor**.
3. **Attendee flag:** an attendee that is not cancelled gets `legacy_violation = true` when its student has an earlier, not cancelled, not flagged attendee whose session overlaps this one. `no_show` counts as holding the slot.
4. The two flags are independent. They mirror the three constraints exactly: room and tutor on `sessions`, student on `attendees`.

On this export the result is: **L034's session** (tutor T1 in R1 and R2 at 03-10 09:00) and **L008's attendee** (Le Minh Chau in R3 and R2 at 03-04 09:00). Nothing else.

## Policy settings

A `BookingPolicy` options section in `appsettings.json`, so a misread number is a config change (DECISIONS §1 assumptions):

| Key | Value |
|---|---|
| `TimeZone` | `Asia/Ho_Chi_Minh` |
| `CutoffLocalTime` | `16:00` |
| `LateCancellationWindow` | `04:00:00` |

Phase 11 adds the other rules (6 a day, opening hours, closed day) to the same section.

## Expected result on this export

| Table | Rows |
|---|---|
| `tutors` | 3 |
| `students` | 6 |
| `sessions` | 33 (34 rows, the exam pair shares one). 2 cancelled (L005, L017). 1 `legacy_violation` (L034). |
| `attendees` | 34. 2 `cancelled`, 1 `no_show` (L015). 1 `legacy_violation` (L008). 0 `chargeable`. |
| `booking_changes` | 2 `cancelled` (L005 by `family`, L017 by `tutor`), both `after_cutoff`. |

## Technical notes

- Npgsql writes `timestamptz` only from a `DateTimeOffset` with offset 0. Build the local time with the +07:00 offset, then store `.ToUniversalTime()`. Reads come back in UTC. Converting back to local time for display is phase 10's job.
- Split the loader in two: a **pure planner** (CSV rows → entities, no I/O, no database) and a **writer** (one transaction, `AddRange`, `SaveChanges`). The planner is what the unit tests cover.
- The seed changes no decision in `DECISIONS.md`, but it adds two small assumptions (below), so `DECISIONS.md` is updated in the same commit.

## Assumptions added to DECISIONS §1

- **Who cancelled is read from the note** ("family cancelled" → family, "tutor sick" → tutor). No keyword → unknown.
- **When two seeded rows overlap at the same start time, the higher lesson ID is the "later" one** (L008 after L007, L034 after L033).

## Context

- `DECISIONS.md` §1 (contradictions, assumptions), §3 (data model, seed of cancelled rows, seeded history vs. the constraints).
- `specs/tech-stack.md`: "Seed history vs. constraints".
- Phase 7 spec: `specs/2026-09-27-exclusion-constraints/`.
