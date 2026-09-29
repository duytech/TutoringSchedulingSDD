# Phase 16 (+17): React Today view — Validation

The phase can be merged when every item below is true.

## Automated

- [ ] `npm test` in `web/` passes (the layout cases in `requirements.md`).
- [ ] `npm run build` in `web/` succeeds with no type errors.
- [ ] `dotnet build` (zero warnings) and `dotnet test` still pass.

## In the browser (API on a freshly seeded dev DB, `npm run dev`)

| # | Check | Expected |
|---|---|---|
| 1 | Open the page | 03-06, now 10:00, the rows in `requirements.md` for 03-06 |
| 2 | `←` three times, to 03-03 | L005 struck through, with the badge. The URL has `?date=2026-03-03` |
| 3 | 03-04 | The pair is one card with two students. L008 has `⚑` and its tooltip |
| 4 | 03-05 | L017 struck through with the badge. L015 is not struck through |
| 5 | 03-10 | L034 has `⚑` |
| 6 | 03-12 | Empty grid and `No sessions on this day.` |
| 7 | `Today` | Back to 03-06, and `?date=` is gone from the URL |
| 8 | Reload on `?date=2026-03-04` | Still 03-04 |
| 9 | Cancel L020 by `family` with curl, then reload 03-06 | L020 struck through, with the badge |
| 10 | Book T2, R3, 03-07 09:00 after cancelling L028 (curl), then open 03-07 | The cancelled L028 and the new session sit side by side in R3 |
| 11 | Stop the API and reload | The API error message |
| 12 | At 1280 × 800 | The whole day fits, with no horizontal scroll |
| 13 | Dark mode | Readable |

## Docs and commit

- [ ] `DECISIONS.md` §2 and §4 are true again: 16 and 17 were built after the time box, with the author's real extra time.
- [ ] `specs/roadmap.md` status: 16 and 17 built (17 merged into 16), 18 and 19 not.
- [ ] `README.md` has the Web section, and every command in it works.
- [ ] `specs/tech-stack.md` Frontend row mentions Vitest.
- [ ] `web/node_modules` and `web/dist` are not committed.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
- [ ] The dev DB is reseeded to the export afterwards.
