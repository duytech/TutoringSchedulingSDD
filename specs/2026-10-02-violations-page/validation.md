# Phase 25: Violations page — Validation

## Automated

- [ ] `npm run lint` in `web/` is clean (oxlint and ESLint).
- [ ] `npm run build` in `web/` succeeds.
- [ ] `npm test` in `web/` passes, including the new `ruleLabel` and `groupByDate` tests.
- [ ] `dotnet build` and `dotnet test` at the root still pass. No API file changed.

## Browser (Playwright CLI, API and `npm run dev`, seeded dev DB)

| # | Check | Expected |
|---|---|---|
| 1 | `/rooms?date=2026-03-06`, click "Violations" | `/violations` opens without a page reload |
| 2 | `/violations` | "4 rule breaks". Four date headings, Wednesday 4 March to Tuesday 10 March 2026, with the rules and lessons in the table in `requirements.md` |
| 3 | The 2026-03-04 item | "Student overlap", then "Le Minh Chau is in R3 with T3 and in R2 with T2 at 09:00.", then "L007 · L008" |
| 4 | Set From 2026-03-05 and To 2026-03-09 | URL is `/violations?from=2026-03-05&to=2026-03-09`. "2 rule breaks": tutor load on 03-06 and closed day on 03-09 |
| 5 | Reload on that URL | Same 2 items, and the inputs show the dates |
| 6 | Set From 2026-03-11 and To 2026-03-12 | "No rule breaks in this range." |
| 7 | Set From 2026-03-10 and To 2026-03-04 | "From must be on or before To." No list, and the inputs keep their values |
| 8 | "Clear" | URL is `/violations`, and all 4 items show again |
| 9 | Change the dates a few times, then press Back | Returns to `/rooms?date=2026-03-06`, not to an earlier range |
| 10 | "← All rooms" | `/rooms` |
| 11 | API stopped, reload `/violations` | "Cannot reach the API" |

## Commit

- [ ] Exactly one commit.
