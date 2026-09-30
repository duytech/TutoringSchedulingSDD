# Phase 22: Tutor day as its own page — Validation

## Automated

- [ ] `npm run lint` in `web/` is clean (oxlint and ESLint).
- [ ] `npm run build` in `web/` succeeds.
- [ ] `npm test` in `web/` passes.
- [ ] `App.tsx` does not import from `api.ts`: only the pages fetch.

## Browser (Playwright CLI, API and `npm run dev`, seeded dev DB)

| # | Check | Expected |
|---|---|---|
| 1 | `/?date=2026-03-06` | Six room columns. The tutor line reads `T1 … 7 · T2 … 2 · T3 … 1`, as before |
| 2 | ← / Today / → on the grid | The date changes, "loading…" shows while fetching, and the old grid stays until the new one arrives |
| 3 | Click `T1` in the tutor line | T1's day opens without a page reload. URL is `?tutor=T1&date=2026-03-06` |
| 4 | ← / → on the tutor page | Stays on T1 and moves the date |
| 5 | "← All rooms" | Back to the grid on the same date |
| 6 | Reload on `?tutor=T1&date=2026-03-06` | The tutor page again |
| 7 | `/?tutor=T9` | "No tutor T9." with a link back to all rooms |
| 8 | Click T1, then T2 straight away | Only T2's sheet shows, never T1's under T2's URL |
| 9 | API stopped | "Cannot reach the API" on both pages |

## Commit

- [ ] Exactly one commit.
