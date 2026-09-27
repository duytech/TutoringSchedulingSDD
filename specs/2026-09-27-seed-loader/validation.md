# Phase 8: Seed loader — Validation

The phase can be merged when every item below is true.

## Automated

- [ ] `dotnet build` succeeds with zero warnings.
- [ ] `dotnet test` passes: the existing tests, `SeedPlannerTests` and the `BookingPolicy` tests.

## Seed runs once

- [ ] From a dropped dev DB, `dotnet run` applies the migrations, seeds, logs the summary line, and `/health` is `Healthy`.
- [ ] A second `dotnet run` seeds nothing: the row counts do not change.
- [ ] With `Seed:Enabled=false`, a fresh database stays empty apart from `rooms`.
- [ ] The insert did not fail on any `EXCLUDE` constraint. This proves the `legacy_violation` flags are enough for the constraints.

## Data (psql)

| # | Check | Expected |
|---|---|---|
| 1 | Row counts: `tutors`, `students`, `sessions`, `attendees`, `booking_changes` | 3, 6, 33, 34, 2 |
| 2 | `tutors` has no phone column or value | Only `id`, `name`, `subject` |
| 3 | Attendees for L009 and L010 | Same `session_id`. That session is T1, R1, 2026-03-04 11:00–12:30 +07 |
| 4 | Sessions with `legacy_violation` | Only the one holding L034 |
| 5 | Attendees with `legacy_violation` | Only L008 |
| 6 | L005 attendee | `cancelled`, `cancelled_by = family`, `chargeable = false`. Session `cancelled_at` = 2026-03-03 08:15 +07 |
| 7 | L017 attendee | `cancelled`, `cancelled_by = tutor`, `chargeable = false`. Session cancelled |
| 8 | `booking_changes` | Two `cancelled` rows (L005, L017), `changed_by` family / tutor, both `after_cutoff = true` |
| 9 | L015 attendee | `no_show`, its session not cancelled |
| 10 | L018 session, shown with `SET TIME ZONE 'Asia/Ho_Chi_Minh'` | `starts_at` 2026-03-06 09:00, `ends_at` 10:00 |
| 11 | Every attendee's `slot` equals its session's `slot` | 0 mismatches (the phase 7 trigger ran) |
| 12 | Every attendee has a `source_lesson_id`, L001 to L034 with no gaps | 34 distinct values |

## Constraints still guard the seeded week (psql, `BEGIN; … ROLLBACK;`)

| # | Case | Expected |
|---|---|---|
| 13 | New session T1 in R3, 2026-03-04 09:00–10:00 (R3 is held by L007, T1 is free) | Refused by `ex_sessions_room_slot` |
| 14 | New session T1 in R3, 2026-03-10 09:00–10:00 (T1 is held by L033, not by the flagged L034) | Refused by `ex_sessions_tutor_slot` |
| 15 | New session T1 in R4, 2026-03-04 09:30–10:30 (both free), then Le Minh Chau added to it (held by L007, not by the flagged L008) | Session accepted. Attendee refused by `ex_attendees_student_slot` |

## Docs and commit

- [ ] `DECISIONS.md` §1 has the two new assumptions (who cancelled comes from the note, and a tie on start time is broken by lesson ID).
- [ ] `specs/tech-stack.md` says the flags are computed and that the constraints check them on insert.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
