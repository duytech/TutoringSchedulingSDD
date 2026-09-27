# Phase 9: Seed violation report — Requirements

Roadmap: Stage B, phase 9 (est. 7 min). One atomic commit.

## Goal

`GET /api/reports/violations` lists every rule the loaded schedule breaks, in plain words. The export is history and is loaded as it is (phase 8). This report is where its rule breaks become visible: the student in two rooms, the tutor in two rooms, the tutor with 7 lessons, and the Monday lesson (`DECISIONS.md` §1, "Where the brief and the data disagree").

The rules are written **once**, as a C# rule set. Phase 11 runs the same rules on a new booking to build its 409, so the report and the 409 can never disagree.

## In scope

- `ScheduleRules`: a pure rule set. Given sessions with their attendees, it returns the violations. No I/O, no database.
- The rest of the policy numbers in the `BookingPolicy` config section (below). They move here from phase 11, because the report needs them first.
- The endpoint, with optional `from` / `to` dates.
- Unit tests for the rules: on the real export (through `SeedPlanner`, no database), and on small hand-made cases for the rules the export never breaks.
- A request in `BrightPath.Api.http`.

## Out of scope

- Late cancellations and changes after the cut-off. They are allowed, not rule breaks. They are already in `attendees.chargeable` and `booking_changes`, and the schedule read (phase 10) shows them.
- Session length (60 or 90 minutes). The database `CHECK` makes it impossible to break.
- The 409 on create → phase 11 (it reuses `ScheduleRules`).
- An API-level test on a real database → phase 12.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Engine | One C# rule set, shared with phase 11. | Each rule is defined in one place. The report and the 409 cannot drift apart. It can be tested on the planned seed, without a database. |
| Shape | One item per violation, listing every lesson involved. | An overlapping pair is one problem, not two. T1's 7 lessons are one problem. The export gives 4 items. |
| Scope | Rule breaks only. | A late cancellation or a change after the cut-off is allowed. Mixing it in would bury the real problems. |
| Filter | Optional `from` / `to` (local dates, inclusive). Without them, everything. | Covers the whole export by default, and still works when there are months of data. |

## Rules

A session is **active** while `cancelled_at` is null. An attendee **holds its slot** unless it is `cancelled`, so `no_show` counts. Cancelled sessions and cancelled attendees are ignored by every rule. `legacy_violation` is **not** used: the report shows every real break, including the rows the constraints skip.

Times are compared in local time (`BookingPolicy.TimeZone`). Overlap is half-open `[start, end)`, the same as the constraints: 09:00–10:00 and 10:00–11:00 do not overlap.

| Code | Breaks when | Reported as |
|---|---|---|
| `room-overlap` | Two active sessions in the same room overlap. | One item per clashing pair |
| `tutor-overlap` | Two active sessions with the same tutor overlap. | One item per clashing pair |
| `student-overlap` | A student holds two attendees whose sessions overlap. | One item per clashing pair |
| `tutor-load` | A tutor has more than `MaxSessionsPerTutorPerDay` active sessions on one local date. An exam pair is one session (Q1). | One item per tutor and date, listing all their sessions that day |
| `closed-day` | An active session is on a day in `ClosedDays`. | One item per session |
| `outside-hours` | An active session starts before `OpensAt` or ends after `ClosesAt`. | One item per session |
| `too-many-attendees` | A session has more than `MaxAttendeesPerSession` attendees that hold their slot. | One item per session |

- The codes are stable. Phase 11 uses them as the conflict kinds in its 409. The three overlap codes match the three exclusion constraints.
- `room-overlap`, `outside-hours` and `too-many-attendees` are never broken by this export. They are still in the rule set, because phase 11 needs them and the owner may change the numbers (Q3, Q7).

## Policy settings added to `BookingPolicy`

| Key | Value | Source |
|---|---|---|
| `MaxSessionsPerTutorPerDay` | `6` | Brief. Counts sessions, not students (Q1). |
| `MaxAttendeesPerSession` | `2` | Exam pairs (Q7). |
| `ClosedDays` | `["Monday"]` | Brief: open Tuesday to Sunday. |
| `OpensAt` | `09:00` | What the centre actually ran (Q3). |
| `ClosesAt` | `21:30` | What the centre actually ran (Q3). |

## Endpoint

`GET /api/reports/violations?from=2026-03-03&to=2026-03-10`

- `from` and `to` are optional local dates, both inclusive. A session is in range when its local start date is. Missing `from` means no lower bound, and missing `to` means no upper bound.
- `from` after `to` → **400** `ProblemDetails`.
- **200** with:

```json
{
  "violations": [
    {
      "rule": "student-overlap",
      "date": "2026-03-04",
      "sessionIds": ["…", "…"],
      "lessonIds": ["L007", "L008"],
      "message": "Le Minh Chau is in R3 with T3 and in R2 with T2 at 09:00."
    }
  ]
}
```

- `lessonIds` are the `source_lesson_id` values, which are what Mai knows. A session booked in the app has none, so `sessionIds` is always there too.
- `date` is the local date of the earliest session in the item.
- Order: by `date`, then by rule in the table order above, then by the first lesson or session.
- `message` is one plain sentence that names people and rooms, with local times.

## Expected result on this export (no filter)

| # | Rule | Date | Lessons |
|---|---|---|---|
| 1 | `student-overlap` | 2026-03-04 | L007, L008 (Le Minh Chau, R3 with T3 and R2 with T2, 09:00) |
| 2 | `tutor-load` | 2026-03-06 | L018, L021, L022, L024, L025, L026, L027 (T1, 7 sessions, limit 6) |
| 3 | `closed-day` | 2026-03-09 | L032 (Monday) |
| 4 | `tutor-overlap` | 2026-03-10 | L033, L034 (T1 in R1 and R2, 09:00) |

Nothing else. In particular, the cancelled L005 and L017 and the no-show L015 break no rule.

## Technical notes

- `ScheduleRules` works on a small read model (session: id, tutor with name, room, start, end, cancelled or not, and attendees with student name, status and lesson ID). It does not take EF entities, so the report query, the planned seed and the phase 11 candidate can all build one.
- The report loads the sessions in range with their attendees, students and tutor in one query (`AsNoTracking`), then runs the rules in memory. That is fine for the data a centre has. A range keeps it bounded.
- The endpoint sits in its own `MapGroup("/api/reports")`, with OpenAPI metadata.

## Docs

- `DECISIONS.md` §3 API table: the report row names the response shape and the filter. One bullet under "Where each rule is enforced": the code rules live in one rule set, shared by the report and the 409.
- `specs/tech-stack.md` "Repository layout": the seed CSVs are in `src/BrightPath.Api/Seed/`, not `/seed/`. Phase 8 moved them and did not update this line.
- `specs/roadmap.md` phase 11: it reuses `ScheduleRules` and the policy settings from phase 9.

## Context

- `DECISIONS.md` §1 (questions Q1, Q3, Q7, contradictions), §3 (rule placement, API).
- Phase 8 spec: `specs/2026-09-27-seed-loader/`.
