# Phase 7: Schema, exclusion constraints — Validation

The phase can be merged when every item below is true.

## Automated

- [ ] `dotnet build` succeeds with zero warnings.
- [ ] `dotnet test` passes, including `SchemaTests` (no pending model changes).

## Migration applies and reverts

- [ ] From a dropped dev DB, `dotnet run` applies `InitialSchema` and `ExclusionConstraints`, and `/health` is `Healthy`.
- [ ] `dotnet ef database update InitialSchema` removes the constraints, trigger, function and `slot` columns with no error. `dotnet ef database update` puts them back.
- [ ] `\dx` lists `btree_gist`.
- [ ] `\d sessions` shows `slot tstzrange` as a generated column, plus `ex_sessions_room_slot` and `ex_sessions_tutor_slot`.
- [ ] `\d attendees` shows `slot tstzrange not null`, `ex_attendees_student_slot` and `trg_attendees_copy_slot`.

## Constraints behave (psql, each case in `BEGIN; … ROLLBACK;`)

Setup for each case: tutors T1 and T2, students A and B, and session S1 = T1 in R1, 2026-03-06 09:00–10:00 +07, with A booked.

| # | Case | Expected |
|---|---|---|
| 1 | Insert an attendee without giving `slot` | Accepted. `attendees.slot` equals `sessions.slot`. |
| 2 | T2 in R1, 09:30–10:30 | Refused by `ex_sessions_room_slot` |
| 3 | T1 in R2, 09:30–10:30 | Refused by `ex_sessions_tutor_slot` |
| 4 | T2 in R2, 09:30–10:30, with A | Session accepted. Attendee refused by `ex_attendees_student_slot` |
| 5 | T2 in R1, 10:00–11:00 (touching, not overlapping) | Accepted |
| 6 | B added to S1 (exam pair) | Accepted |
| 7 | S1 cancelled (`cancelled_at` set), then T2 in R1, 09:00–10:00 | Accepted |
| 8 | A's attendee cancelled, then A booked with T2 in R2 at 09:00 | Accepted |
| 9 | A's attendee `no_show`, then A booked with T2 in R2 at 09:00 | Refused (no-show keeps the slot) |
| 10 | T2 in R1, 09:00–10:00 with `legacy_violation = true` | Accepted |
| 11 | After case 10, a new T2 session in R1, 09:30–10:30 (not legacy) | Refused, because S1 still guards the room |

- [ ] Every case gives the expected result.
- [ ] After the run, `sessions`, `attendees`, `students` and `tutors` are empty (ready for the phase 8 seed).

## Docs and commit

- [ ] `specs/tech-stack.md` shows the real predicates and the trigger.
- [ ] `DECISIONS.md` §3 mentions the trigger and the "cancelled session ⇒ cancelled attendees" invariant.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
