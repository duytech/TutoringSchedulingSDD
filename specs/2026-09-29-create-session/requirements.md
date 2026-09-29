# Phase 11: Create session — Requirements

Roadmap: Stage C, phase 11 (est. 15 min). One atomic commit.

## Goal

`POST /api/sessions` books a new session, or refuses it with a **409** that lists **every** rule it breaks, in plain words. This is the write where a double-booking gets in, so it is the heart of the feature (`DECISIONS.md` §2).

The code rules are the ones in `ScheduleRules` (phase 9), run on the new session plus that day's sessions, so the 409 and the violation report always agree. The database exclusion constraints (phase 7) are the backstop: if a race gets past the code checks, the constraint error becomes the same 409.

## In scope

- `POST /api/sessions`: validate, check, insert the session, its attendees and a `created` change, in one transaction.
- `GET /api/sessions/{id}`: one session, in the same shape as an item of `/api/schedule`. `Location` points here.
- A pure booking check: the new session's conflicts (the rules plus "not in the past"). No I/O, no database.
- Mapping an exclusion-constraint error to a 409.
- Unit tests for the booking check, on the real export (through `SeedPlanner`) and on hand-made cases.
- Requests in `BrightPath.Api.http`.

## Out of scope

- Cancel → phase 13. Move → phase 18.
- Adding a student to an existing session. The model allows it, but there is no endpoint (`DECISIONS.md` §3).
- Creating students or tutors. Students are the ones in the export.
- Overriding a rule (Q5: no override).
- API-level tests on a throwaway database, including the race → phase 12.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Past | A session that starts before the clock's now is refused with **409**, code `in-the-past`, in the same `conflicts` list as the other rules. Starting exactly at now is allowed. It is not in the violation report. | You cannot book a lesson that has already started. The report looks at history, which is all in the past, so the rule only applies to a new write. |
| 201 | A small `GET /api/sessions/{id}`. `Location` points to it, and the 201 body is the same session view. | `Location` should name the resource that was created. The view is the one `/api/schedule` already builds, so there is one shape for a session. |
| Students | `studentIds` (uuids), as `DECISIONS.md` §3 says. Clients find them in `/api/schedule` (every attendee has `studentId`). | No new endpoint. Students are not created here. |
| `changed_by` | The `created` change is written with `changed_by` = `centre`. No new body field. | The receptionist of the centre is the one who books. |

## Request

`POST /api/sessions`

```json
{
  "tutorId": "T2",
  "roomId": "R4",
  "startsAt": "2026-03-07T13:00:00+07:00",
  "durationMin": 60,
  "studentIds": ["…"]
}
```

### 400: bad input (`ValidationProblem`, one error per field)

Checked first. If any of these fail, no rule is run.

| Field | Rule |
|---|---|
| `tutorId` | Required, and a tutor with that id exists. |
| `roomId` | Required, and a room with that id exists. |
| `startsAt` | Required, ISO-8601 **with an offset** (`+07:00` or `Z`). Without one, the server would have to guess the zone, so it is refused. |
| `durationMin` | `60` or `90`. |
| `studentIds` | At least one. No duplicates. Every id is a known student. |

More than `MaxAttendeesPerSession` students is **not** a 400. It is the `too-many-attendees` rule, so it is a 409 like the others.

A missing or malformed JSON body is a 400 from binding, the same as a bad query value. Unknown student ids are named in the error, so the receptionist knows which one is wrong.

## Rules checked (409)

The new session is checked together with every active session on its local date. Only violations that **involve the new session** are reported. A rule the day already broke (T1's 7 sessions on 03-06) is not the new booking's fault, unless the new booking is part of it.

| Code | Source |
|---|---|
| `in-the-past` | New. `startsAt` is before the clock's now. Message: `The session starts at 2026-03-05 11:00, before now (2026-03-06 10:00).` (local times) |
| `room-overlap`, `tutor-overlap`, `student-overlap`, `tutor-load`, `closed-day`, `outside-hours`, `too-many-attendees` | `ScheduleRules` (phase 9). |

- All conflicts are listed at once. `in-the-past` comes first, then the `ScheduleRules` order.
- A `tutor-load` conflict lists every session of that tutor that day, the new one included.
- Seeded rows flagged `legacyViolation` are ordinary active sessions for the rules, so a new booking that overlaps L034 is refused even though the constraint skips L034.
- Cancelled sessions and cancelled attendees hold nothing (the `ScheduleRules` rule), so a freed slot can be booked again.
- Each conflict's `sessionIds` includes the new session's id, even though nothing was saved. It is the id the session would have had.

### 409 body

`ProblemDetails` with a `conflicts` extension. Each conflict is a violation as the report shows it:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.10",
  "title": "The booking breaks centre rules",
  "status": 409,
  "detail": "3 conflicts.",
  "conflicts": [
    {
      "rule": "room-overlap",
      "date": "2026-03-07",
      "sessionIds": ["…", "…"],
      "lessonIds": ["L028"],
      "message": "R3 holds two sessions at once: T3 at 09:00 and T3 at 10:00."
    }
  ]
}
```

### A race past the code checks

Two receptionists can both pass the code checks, and the second insert then hits an exclusion constraint (`23P01`). It is turned into the same 409, with one conflict:

| Constraint | Code | Message |
|---|---|---|
| `ex_sessions_room_slot` | `room-overlap` | `R4 was booked by someone else at the same moment.` |
| `ex_sessions_tutor_slot` | `tutor-overlap` | `T2 was booked by someone else at the same moment.` |
| `ex_attendees_student_slot` | `student-overlap` | `A student was booked by someone else at the same moment.` |

`sessionIds` holds only the new session's id. The transaction is rolled back, so nothing is saved. Any other database error is not caught.

### Tutor load under a race

`tutor-load` is a count, so a constraint cannot guard it. The transaction takes a Postgres advisory lock for the tutor and local date **before** it reads that day's sessions: `pg_advisory_xact_lock(hashtextextended('tutor-day:T1:2026-03-06', 0))`. Two bookings for the same tutor and day then run one after the other, so they cannot both take the 6th slot. Bookings for other tutors or days are not blocked. The lock is released at commit or rollback.

The isolation level stays at Read Committed. Each statement sees what was committed before it started, so the second booking reads the first one's session once the lock is released.

## Success (201)

- One transaction: the lock, read the day, check, then insert the session, its attendees (`booked`) and one `booking_changes` row.
- The change: `kind` = `created`, `attendee_id` null (the whole session), `changed_at` = now, `changed_by` = `centre`, `after_cutoff` from `BookingPolicy.IsAfterCutoff(now, startsAt)`. A booking for today or tomorrow after 16:00 is flagged.
- `Location: /api/sessions/{id}`. The body is the session view (see below).

## `GET /api/sessions/{id}`

- **200** with one session, in the shape of an item of `/api/schedule` `sessions` (attendees, changes, `state`, flags, local times).
- **404** `ProblemDetails` when there is no such session.

## Expected results on this export

The clock is at 2026-03-06 10:00. Student names below stand for their ids (read them from `/api/schedule`). The rows run in order on one freshly seeded database. The valid bookings (1, 2, 3, 12) do not clash with each other, and no refused row depends on one of them, except row 4, which repeats row 1.

| # | Request | Result |
|---|---|---|
| 1 | T2, R4, 2026-03-07 13:00, 60, [Vu Ha My] | 201. One `created` change, `afterCutoff` false (the cut-off for 03-07 is 03-06 16:00) |
| 2 | T2, R4, 2026-03-06 16:00, 60, [Vu Ha My] | 201. `afterCutoff` true: a lesson for today, booked after yesterday's 16:00 |
| 3 | T3, R5, 2026-03-07 14:00, 90, [Nguyen Thi Ha, Do Van Kien] | 201. An exam pair: one session, 2 attendees |
| 4 | Row 1 again, after row 1 | 409. `room-overlap`, `tutor-overlap`, `student-overlap` |
| 5 | T3, R3, 2026-03-07 10:00, 60, [Tran Bao Long] | 409. `room-overlap`, `tutor-overlap`, `student-overlap`, all against L028 |
| 6 | T1, R4, 2026-03-06 14:30, 60, [Do Van Kien] | 409. Only `tutor-load`: T1 would have 8 sessions, limit 6. The conflict lists 8 sessions |
| 7 | T2, R2, 2026-03-09 10:00, 60, [Le Minh Chau] | 409. Only `closed-day` (Monday) |
| 8 | T2, R4, 2026-03-07 20:30, 90, [Vu Ha My] | 409. Only `outside-hours` (ends 22:00) |
| 9 | T2, R6, 2026-03-08 14:00, 60, [Vu Ha My, Le Minh Chau, Bui An Nhien] | 409. Only `too-many-attendees` |
| 10 | T2, R4, 2026-03-05 11:00, 60, [Vu Ha My] | 409. Only `in-the-past` |
| 11 | T2, R4, 2026-03-10 09:00, 60, [Le Minh Chau] | 409. Only `student-overlap`, with L033 |
| 12 | T3, R4, 2026-03-10 09:00, 60, [Vu Ha My] | 201. L034 is `legacyViolation`, but it is T1 in R2, so R4 with T3 is free |

## Technical notes

- **Booking check** (`Domain/BookingCheck.cs`): `Conflicts(candidate, sameDaySessions, now, policy)` returns the `in-the-past` conflict if any, then `ScheduleRules.Check(sameDay + candidate)` filtered to violations whose `SessionIds` contain the candidate. It uses `RuleSession`, so it can be tested on the planned seed.
- `RuleCodes.InThePast` is a new constant. It is **not** in `RuleCodes.All`, because `ScheduleRules` never produces it.
- The day's sessions are loaded with the same projection as the violation report. Move that projection into one shared query helper, so the report and the create cannot load sessions differently.
- The session view for the 201 and for `GET /api/sessions/{id}` reuses `ScheduleDay`: make its per-session view public. Move the `DaySession` projection that `/api/schedule` uses into a shared helper too.
- `startsAt` is bound as a string and parsed in the handler, so a value without an offset can be refused. `System.Text.Json` would otherwise accept it and use the server's zone.
- The new session's id is created in code (`Guid.CreateVersion7()`) before the check, so the check can tell which violations involve it.
- Endpoints go in `Endpoints/SessionEndpoints.cs`, in a `MapGroup("/api/sessions")`. Phase 13 adds cancel to the same group.

## Docs

- `DECISIONS.md` §3 API table: the `POST /api/sessions` row names `in-the-past` and `Location`. A new row for `GET /api/sessions/{id}`.
- `DECISIONS.md` §3 "Where each rule is enforced": a row for "a booking cannot start in the past" (code, against the pinned clock, create only).
- `DECISIONS.md` §3 change record: a new booking's `created` change is written with `changed_by` = `centre`.
- `specs/roadmap.md` phase 11: also `GET /api/sessions/{id}` and the `in-the-past` rule.

## Context

- `DECISIONS.md` §1 (Q1, Q5, Q7), §3 (rule placement, change records, API).
- Phase 9 spec (`ScheduleRules`), phase 10 spec (clock, session view).
