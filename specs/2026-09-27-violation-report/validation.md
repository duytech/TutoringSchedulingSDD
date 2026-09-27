# Phase 9: Seed violation report — Validation

The phase can be merged when every item below is true.

## Automated

- [ ] `dotnet build` succeeds with zero warnings.
- [ ] `dotnet test` passes: the existing tests and `ScheduleRulesTests`.
- [ ] On the real export, the rules return exactly the 4 items from `requirements.md`, in order.
- [ ] Each hand-made case in `plan.md` step 4 passes, on both sides of its edge.

## Endpoint (curl, on the seeded dev DB)

| # | Request | Expected |
|---|---|---|
| 1 | `GET /api/reports/violations` | 200. 4 items: `student-overlap` 03-04 (L007, L008), `tutor-load` 03-06 (L018, L021, L022, L024, L025, L026, L027), `closed-day` 03-09 (L032), `tutor-overlap` 03-10 (L033, L034) |
| 2 | Same, check every item | `sessionIds` has one id per session (1 for L032, 2 for each pair, 7 for the load item). `message` names people and rooms, with local times (09:00, not 02:00) |
| 3 | `?from=2026-03-06&to=2026-03-06` | Only the `tutor-load` item |
| 4 | `?from=2026-03-09` | `closed-day` and `tutor-overlap` |
| 5 | `?to=2026-03-05` | Only `student-overlap` |
| 6 | `?from=2026-03-11` | 200, empty list |
| 7 | `?from=2026-03-10&to=2026-03-03` | 400 `ProblemDetails` |
| 8 | `?from=not-a-date` | 400 |
| 9 | Scalar UI / `/openapi/v1.json` | The endpoint is listed, with its query parameters and responses |

## Docs and commit

- [ ] `DECISIONS.md` §3 describes the report (shape, filter) and says the code rules live in one rule set shared with the 409.
- [ ] `specs/tech-stack.md` shows the seed CSVs in `src/BrightPath.Api/Seed/`.
- [ ] `specs/roadmap.md` phase 11 says it reuses `ScheduleRules`.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
