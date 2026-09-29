# Phase 11: Create session — Validation

The phase can be merged when every item below is true.

## Automated

- [ ] `dotnet build` succeeds with zero warnings.
- [ ] `dotnet test` passes: the existing tests and `BookingCheckTests`.
- [ ] Rows 4–12 of the table in `requirements.md` pass as unit tests on the real export.
- [ ] The edges in `plan.md` step 5 pass on both sides.

## Endpoint (curl, on a freshly seeded dev DB)

Student ids come from `GET /api/schedule?date=…`.

| # | Request | Expected |
|---|---|---|
| 1 | Row 1 (T2, R4, 03-07 13:00, Vu Ha My) | 201. `Location: /api/sessions/{id}`. Body: one attendee, one `created` change, `changedBy` `centre`, `afterCutoff` false |
| 2 | `GET` the `Location` | 200, the same view |
| 3 | `GET /api/schedule?date=2026-03-07` | The new session is in R4 and T2's lists |
| 4 | Row 1 again | 409. `room-overlap`, `tutor-overlap`, `student-overlap`, each with plain messages |
| 5 | Row 2 (today 16:00) | 201. `afterCutoff` true, `changedAfterCutoff` true |
| 6 | Row 3 (exam pair) | 201. 2 attendees |
| 7 | Row 5 (T3, R3, 03-07 10:00) | 409. Three conflicts, all naming L028 |
| 8 | Row 6 (T1 on 03-06) | 409. Only `tutor-load`, 8 sessions, message says 8 and limit 6 |
| 9 | Rows 7, 8, 9, 10, 11 | 409, each with only its one conflict |
| 10 | Row 12 (T3, R4, 03-10 09:00) | 201 |
| 11 | `durationMin` 45 / unknown `tutorId` T9 / unknown room / unknown student id / empty `studentIds` / duplicate ids / `startsAt` `2026-03-07T13:00:00` (no offset) | 400 `ValidationProblem`, naming the field |
| 11b | No body / body `{` | 400 |
| 12 | `GET /api/sessions/{random guid}` | 404 |
| 13 | `GET /api/reports/violations` after all of the above | Still exactly the 4 items of the export: the new bookings break nothing |
| 14 | Scalar UI / `/openapi/v1.json` | Both endpoints listed, with their responses |

## Not checked here

- The race (constraint error → 409, and the advisory lock) needs two parallel requests. It is covered by the phase 12 tests.

## Docs and commit

- [ ] `DECISIONS.md` §3 has the `POST` and `GET /api/sessions/{id}` rows, the "in the past" rule, and `changed_by` = `centre`.
- [ ] `specs/roadmap.md` phase 11 names `GET /api/sessions/{id}` and `in-the-past`.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
- [ ] The dev DB is reseeded to the export afterwards.
