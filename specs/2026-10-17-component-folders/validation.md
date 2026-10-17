# Phase 27: One folder per web component — Validation

## Automated (in `web/`)

- [ ] `npm run build` succeeds.
- [ ] `npm run lint` reports nothing.
- [ ] `npm test` passes, with the new `structure.test.ts`.

## Structure

| # | Check | Expected |
|---|---|---|
| 1 | `ls src/pages src/components` | 4 page folders, 8 component folders, as in the component map |
| 2 | Each folder | `<Name>.tsx` and `<Name>.css`; `LegacyRedirect` has only `LegacyRedirect.tsx` |
| 3 | `ls src/*.tsx` | `App.tsx` and `main.tsx` only |
| 4 | `grep -r "App.css" src` | No match, and `src/App.css` is gone |
| 5 | `grep -rn "index.ts" src/pages src/components` | No barrel files |

## No style lost

| # | Check | Expected |
|---|---|---|
| 1 | Selectors in `dist/assets/*.css`, before vs after | The same set: none lost, none added |
| 2 | Each `App.css` selector | Appears in exactly one of the new CSS files |

## Browser (Playwright CLI, API and `npm run dev`, light and dark mode)

Each screenshot must match the one taken before the change.

| # | Page | Expected |
|---|---|---|
| 1 | `/rooms?date=2026-03-06` | Header with the day arrows, the tutor line `T1 … 7 · T2 … 2 · T3 … 1`, six room columns, the now line, cards with badges and moved links |
| 2 | Click a card's moved link | Goes to the other day, the grid looks the same |
| 3 | `/tutors/T1?date=2026-03-06` | The "Changed after you were told" box, then the day as row cards; past cards faded, cancelled cards with a grey edge |
| 4 | `/violations` | The range form in the header, one section per date, rule labels as pills |
| 5 | Window at 600px wide on `/violations` | The header wraps and each violation stacks, as before |
| 6 | `/?tutor=T1&date=2026-03-06` | Redirects to `/tutors/T1?date=2026-03-06` |
| 7 | `/tutors/T9` | The error message box, styled as before |

## Docs and commit

- [ ] `CLAUDE.md` has the frontend folder rule.
- [ ] `git log --follow src/components/SessionCard/SessionCard.tsx` shows the history from before the move.
- [ ] Exactly one commit.
