# Roadmap

Very small phases, in order. **One phase = one atomic commit** with a "what + why" message. Docs come first, then code, so the git history shows each decision *before* the code that follows from it.

Time box: **150 min**. Estimates are in minutes. Everything above the ✂️ cut line is the must-ship scope.

## Stage A: Read and decide (docs)

| # | Phase | Output | Est |
|---|---|---|---|
| 1 | DECISIONS §1: questions for the owner | `DECISIONS.md`: questions for the owner and how each answer changes the build (pair counting, who pays for a tutor cancellation, opening hours, room count) | 8 |
| 2 | DECISIONS §1: contradictions + assumptions | Contradictions found in the brief and the data (exam pair, Monday L032, T1 overload, L034, hours), the reading chosen for each, and invented assumptions (pinned date 2026-03-06) | 7 |
| 3 | DECISIONS §2: feature list + pick | ~6 one-line features. Pick "Conflict-safe schedule", argue for it, and list what is left broken | 10 |
| 4 | DECISIONS §3: design | Data model (Session / Attendee / BookingChange), how a cancel or move after notification is represented, the DB vs. code rule split, API shape, and one rejected endpoint | 15 |

## Stage B: Skeleton and data

| # | Phase | Output | Est |
|---|---|---|---|
| 5 | Scaffold | Solution, Minimal API project, test project, connection string to local Postgres (`appsettings.Development.json` + user-secrets override), `/health` checks the DB | 8 |
| 6 | Schema: core tables | EF entities + migration: tutors, rooms (R1–R6), students, sessions, attendees, booking_changes, with CHECKs | 10 |
| 7 | Schema: exclusion constraints | Raw SQL migration: `btree_gist`, generated tstzrange `slot`, EXCLUDE on room, tutor and student (partial `WHERE active AND NOT legacy_violation`) | 8 |
| 8 | Seed loader | Import the CSVs. Group rows into Sessions (the exam pair becomes 1 session with 2 attendees). Map `status` and `cancelled_at`. Mark `legacy_violation` | 12 |
| 9 | Seed violation report | `GET /api/reports/violations` lists every rule the imported week broke | 7 |

## Stage C: The feature

| # | Phase | Output | Est |
|---|---|---|---|
| 10 | Pinned clock + Today read | Fixed `TimeProvider` (2026-03-06 10:00 +07:00). `GET /api/schedule?date=` (defaults to today), grouped by room and tutor | 8 |
| 11 | Create session | `POST /api/sessions` (+ `GET /api/sessions/{id}` for `Location`) with code rules, plus `in-the-past` against the pinned clock (Monday/hours, 6 per tutor per day, ≤2 attendees), checked by the `ScheduleRules` and `BookingPolicy` settings from phase 9. A DB exclusion violation maps to **409 ProblemDetails** listing the conflicts | 15 |
| 12 | Integration tests: conflicts | Throwaway test DB on local Postgres (created and dropped per run). One test each for student, room, tutor, tutor-load, Monday, and a valid exam pair. Plus one `GET /api/schedule` test on the pinned day, and one with `Clock:Now` moved (phase 10 only has unit tests for the builder) | 12 |
| 13 | Cancel attendee | `POST /api/sessions/{id}/attendees/{attendeeId}/cancel` with `cancelledBy` (family/tutor/centre). Late (<4h) → `chargeable`. The slot is freed. A `BookingChange` is written with `after_cutoff` | 12 |
| 14 | README | Prerequisites (local PostgreSQL, `btree_gist`, DB user rights, how to set the connection string), run commands (`dotnet run`, `dotnet test`) and **what I saw** (seed violations, sample 409) | 5 |
| 15 | DECISIONS §4: reflection | Next week's work, known weak spots, where the AI helped, one AI suggestion rejected and why | 10 |
| | **Subtotal** | | **147** |

---

### ✂️ Cut line: only continue if Stage C finished early

| # | Phase | Output | Est |
|---|---|---|---|
| 16 | React Today view | Vite + TS, one page: room × time grid for the pinned date, with cancelled lessons struck through | 20 |
| 17 | Changes badge in UI | Mark lessons with an `after_cutoff` change ("changed after tutor was told") | 8 |
| 18 | Move session | `POST /api/sessions/{id}/move` = cancel the old session + create a new one linked through `moved_to_session_id`, in one transaction | 12 |
| 19 | Tutor day endpoint | `GET /api/tutors/{id}/day?date=` gives one tutor's authoritative day, including changes | 8 |

If phase 16 is skipped, `DECISIONS.md` §4 says so honestly, and the Today view is still available as JSON from phase 10.

## Definition of done (every phase)

- It builds, and the existing tests pass.
- There is one commit whose message says what changed and why.
- If a decision changed, `DECISIONS.md` is updated in the **same** commit.
