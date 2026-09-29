# Phase 10: Pinned clock + Today read — Validation

The phase can be merged when every item below is true.

## Automated

- [ ] `dotnet build` succeeds with zero warnings.
- [ ] `dotnet test` passes: the existing tests and `ScheduleDayTests`.
- [ ] On the real export, 2026-03-06 gives exactly the room and tutor indexes from `requirements.md`.
- [ ] Each hand-made case in `plan.md` step 5 passes, on both sides of its edge.
- [ ] No production code calls `DateTime.Now`, `DateTime.UtcNow` or `DateTimeOffset.UtcNow` (grep).

## Endpoint (curl, on the seeded dev DB)

| # | Request | Expected |
|---|---|---|
| 1 | `GET /api/schedule` | 200. `date` is `2026-03-06`, `now` is `2026-03-06T10:00:00+07:00`. 10 sessions |
| 2 | Same, check the indexes | R1 has 7 sessions, R2 has 2, R3 has 1, R4–R6 are empty. T1 has 7, T2 has 2, T3 has 1 |
| 3 | Same, check `state` | L018 and L019 are `past`. The other 8 are `upcoming` |
| 4 | Same, check the times | `startsAt` for L018 is `2026-03-06T09:00:00+07:00`, not `02:00Z` |
| 5 | `?date=2026-03-03` | L005: session `cancelled` true, attendee `cancelled` by `family`, `chargeable` false, one `cancelled` change with `afterCutoff` true, `changedAfterCutoff` true |
| 6 | `?date=2026-03-04` | L009 + L010 are one session with 2 attendees. The L008 attendee has `legacyViolation` true, and its session false |
| 6b | `?date=2026-03-10` | The L034 session has `legacyViolation` true, and its attendee false |
| 7 | `?date=2026-03-05` | L015 is `no_show` and its session is not cancelled. L017 is cancelled by `tutor`, `chargeable` false |
| 8 | `?date=2026-03-12` | 200. `sessions` is empty. All 6 rooms and all tutors are listed with empty `sessionIds` |
| 9 | `?date=not-a-date` | 400 |
| 10 | Run with `Clock__Now=2026-03-10T09:30:00+07:00` | `GET /api/schedule` returns 2026-03-10. The 09:00 sessions (L033, L034) are `in-progress` |
| 11 | Scalar UI / `/openapi/v1.json` | The endpoint is listed, with its query parameter and responses |

## Docs and commit

- [ ] `DECISIONS.md` §1 says the pinned clock is `Clock:Now` in config.
- [ ] `DECISIONS.md` §3 describes the schedule response (flat sessions plus room and tutor indexes, cancelled included, `state`, changes).
- [ ] `specs/tech-stack.md` "Clock" row names `Clock:Now`.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
