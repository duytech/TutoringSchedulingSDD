# Phase 24: Each page loads its own data in a plain effect — Validation

## Automated

- [ ] `npm run lint` in `web/` is clean (oxlint and ESLint).
- [ ] `npm run build` in `web/` succeeds.
- [ ] `npm test` in `web/` passes.
- [ ] `useLoad.ts` is gone, and nothing imports it.

## Browser (Playwright CLI, API and `npm run dev`, seeded dev DB)

| # | Check | Expected |
|---|---|---|
| 1 | `/rooms?date=2026-03-06` | Six room columns, ten cards. The tutor line reads `T1 … 7 · T2 … 2 · T3 … 1` |
| 2 | → on the grid | `/rooms?date=2026-03-07` and that day's grid |
| 3 | Click `T1` | T1's sheet on the same date |
| 4 | → on the tutor page | T1 on the next day |
| 5 | `/tutors/T9` | "No tutor T9." |
| 6 | T1, then T2 straight away | Only T2's sheet shows |
| 7 | `fetch` failing, then → | "Cannot reach the API", and the previous page stays on screen |

## Commit

- [ ] Exactly one commit.
