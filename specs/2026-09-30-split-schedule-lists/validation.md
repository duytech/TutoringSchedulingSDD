# Phase 21: Rooms and tutors as their own endpoints — Validation

## Automated

- [ ] `dotnet build BrightPath.slnx` succeeds with zero warnings.
- [ ] `dotnet test tests/BrightPath.Api.UnitTests` passes, with `ReferenceListHandlerTests`.
- [ ] `dotnet test tests/BrightPath.Api.IntegrationTests` passes, with `ReferenceListEndpointTests`.
- [ ] `npm run build` and `npm test` in `web/` pass.

## Endpoint (curl or `.http`, on the seeded dev DB)

| # | Check | Expected |
|---|---|---|
| 1 | `GET /api/rooms` | 200, `R1` to `R6` in order |
| 2 | `GET /api/tutors` | 200, `T1` to `T3` with name and subject |
| 3 | `GET /api/schedule?date=2026-03-06` | 200, keys `date`, `now`, `sessions` only |
| 4 | `/openapi/v1.json` | Has `GetRooms` and `GetTutors`. `ScheduleDayView` has no `rooms` or `tutors` |

## Browser (Playwright CLI, API and `npm run dev`)

| # | Check | Expected |
|---|---|---|
| 1 | The Today view on 03-06 | Six room columns, R1 to R6. The tutor line reads `T1 … 7 · T2 … 2 · T3 … 1` as before |
| 2 | Click a tutor | Their day opens; "← All rooms" goes back to the grid |

## Docs and commit

- [ ] `DECISIONS.md` lists the two new endpoints, and the schedule row has no indexes.
- [ ] `README.md` curl sample works against the new shape.
- [ ] Exactly one commit.
