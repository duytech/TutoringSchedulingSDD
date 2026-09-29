# Phase 18: Move session — Validation

The phase can be merged when every item below is true.

## Automated

- [ ] `dotnet build BrightPath.slnx` succeeds with zero warnings (the web project included).
- [ ] `dotnet test` passes: the existing tests, `MoveCheckTests` and `MoveSessionTests`.
- [ ] `npm test` and `npm run lint` pass in `web/`.
- [ ] The race test fails when the `FOR UPDATE` in the move is removed (checked by hand, not committed), and passes once it is back.

## Endpoint (curl, on a freshly seeded dev DB)

| # | Check | Expected |
|---|---|---|
| 1–13 | Rows 1–13 of the table in `requirements.md`, in order | As listed there |
| 14 | `GET /api/sessions/{old id}` after row 1 | `cancelled`, `movedToSessionId` and `movedTo` set, one `moved` change with the "to …" note |
| 15 | `GET /api/reports/violations` after row 5 | 3 items: the 03-10 `tutor-overlap` is gone |
| 16 | Scalar UI / `/openapi/v1.json` | The move endpoint is listed, with its responses |

## Browser (Playwright CLI, API and `npm run dev`)

| # | Check | Expected |
|---|---|---|
| 1 | 03-07 after row 1 | L028 is struck through with `moved → 14:00 R3`. The new session is in R3 at 14:00 |
| 2 | 03-10 after row 5 | L034 is struck through with `moved → 10:00 R2`. The `⚑` is on the old card only |
| 3 | A move to another day, then its label | Shows the date. Clicking it opens that date, with `?date=` in the URL |
| 4 | The badge tooltip on a late move | `Session: moved by family on …` |

## Docs and commit

- [ ] `DECISIONS.md` §1 has Q8. §3 has the move row and rules. §4 is true: phase 18 is built, with the author's real extra time, next week and weak spots are updated.
- [ ] `specs/roadmap.md` status: phase 18 built.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
- [ ] The dev DB is reseeded to the export afterwards.
