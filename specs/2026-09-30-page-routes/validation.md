# Phase 23: Routes for the room grid and the tutor day — Validation

## Automated

- [ ] `npm run lint` in `web/` is clean (oxlint and ESLint).
- [ ] `npm run build` in `web/` succeeds.
- [ ] `npm test` in `web/` passes.
- [ ] `src/place.ts` is gone, and nothing calls `history.replaceState` or `pushState` by hand.

## Browser (Playwright CLI, API and `npm run dev`, seeded dev DB)

| # | Check | Expected |
|---|---|---|
| 1 | `/` | Redirects to `/rooms` and shows today's grid |
| 2 | `/rooms?date=2026-03-06` | Six room columns. The tutor line reads `T1 … 7 · T2 … 2 · T3 … 1` |
| 3 | Click `T1` | `/tutors/T1?date=2026-03-06`, no page reload. Back returns to `/rooms?date=2026-03-06` |
| 4 | → on the tutor page, then Back | → gives `/tutors/T1?date=2026-03-07`. Back skips the day step and returns to `/rooms?date=2026-03-06` |
| 5 | "← All rooms" on T1's 03-07 | `/rooms?date=2026-03-07` |
| 6 | Old link `/?tutor=T1&date=2026-03-06` | Redirects to `/tutors/T1?date=2026-03-06` |
| 7 | Old link `/?date=2026-03-06` | Redirects to `/rooms?date=2026-03-06` |
| 8 | `/tutors/T9` | "No tutor T9." with a link to all rooms |
| 9 | `/nope` | Redirects to `/rooms` |
| 10 | Reload on `/tutors/T1?date=2026-03-06` | The same page again |
| 11 | Click T1, then T2 straight away | Only T2's sheet shows |

## Docs and commit

- [ ] `README.md` shows the new URLs.
- [ ] Exactly one commit.
