# Phase 25: Violations page — Requirements

Roadmap: not on the roadmap. A web-only change that comes after phase 24. It is one atomic commit.

## Goal

`GET /api/reports/violations` (phase 9) lists every rule the schedule breaks, but today the only way to see it is the `curl` line in the README. Mai should see it in the browser, next to the room grid.

After this phase `/violations` shows the report grouped by date. Each item has its rule, the API's plain-words message and the lessons involved. Two optional dates narrow the range.

## In scope

| File | Change |
|---|---|
| `api.ts` | `ScheduleViolation` and `ViolationReport` types, and `fetchViolations(from?, to?)`, which goes through `getJson`. |
| `labels.ts` | `ruleLabel(code)`: `student-overlap` → "Student overlap" and so on for the 7 codes. An unknown code is shown as it is. |
| `violations.ts` | `groupByDate(violations)`: `[{ date, violations }]`, keeping the API's order. |
| `ViolationsPage.tsx` | The page: header, range inputs, grouped list, empty and error messages. |
| `App.tsx` | Route `/violations`. |
| `DayPage.tsx` | A "Violations" link at the end of the tutor line in the header. |
| `App.css` | Styles for the range inputs and the grouped list. |
| `README.md` | One paragraph in "Web: the Today view" about `/violations`. |

Plus tests for `ruleLabel` and `groupByDate`.

## Out of scope

- Any change to the API or to the report's response.
- Links from a violation to `/rooms?date=…` or `/tutors/…`.
- Resolving, hiding or editing a violation.
- Paging by week, or a default range. Without dates the whole report shows, as the API does.
- A new library (date picker, table, data fetching).

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Filter | Optional `from` / `to` date inputs, kept in the URL as `?from=…&to=…`. Changing them replaces the history entry, the same as `useDate`. | A filtered report can be shared and survives a reload. Back leaves the page instead of undoing every keystroke. |
| Layout | Grouped by date: one heading per date, then its violations. | The API already sorts by date, then rule, then lesson. Mai thinks in days. |
| Navigation | A "Violations" link in the `/rooms` header and "← All rooms" on this page. | The room grid is the home page. Nothing else links in. |
| Header | Its own `<header className="top">`, not `PageHeader`. | `PageHeader` comes with ← / Today / → for one day, and this page covers a range. Same CSS classes, so it looks like the other pages. |
| Loading | A plain effect with the `current` guard. `loading` is "range asked for ≠ range loaded". The old list stays while the next one loads. | The same pattern as `DayPage` and `TutorDayPage` (phase 24). |

## Page

URL: `/violations`, `/violations?from=2026-03-05&to=2026-03-09`. Either date may be missing.

```
Rule breaks                                         [From ____] [To ____] [Clear]
4 rule breaks · ← All rooms

Wednesday 4 March 2026
  Student overlap   Le Minh Chau is in R3 with T3 and in R2 with T2 at 09:00.
                    L007 · L008

Friday 6 March 2026
  Tutor load        T1 Ngoc Anh has 7 sessions on 2026-03-06; the limit is 6.
                    L018 · L021 · L022 · L024 · L025 · L026 · L027
…
```

- The date heading uses `longDate` from `dates.ts`.
- Lesson IDs are joined with " · ". An item with no lesson IDs (only sessions booked in the app) shows none.
- The count line: "1 rule break" or "N rule breaks". While loading, " · loading…" as on the other pages.
- "Clear" removes both dates. It is disabled when neither is set.

## States

| State | Shown |
|---|---|
| No violations in range | "No rule breaks in this range." (or "No rule breaks." without a range) |
| `from` after `to` (API 400) | "From must be on or before To." The inputs keep their values. The list is hidden. |
| API down, or any other error | `ApiErrorMessage`, as on the other pages |

## Expected on the seeded data (no range)

Taken from the phase 9 spec, "Expected result on this export":

| # | Date | Rule | Lessons |
|---|---|---|---|
| 1 | 2026-03-04 | Student overlap | L007 · L008 |
| 2 | 2026-03-06 | Tutor load | L018 · L021 · L022 · L024 · L025 · L026 · L027 |
| 3 | 2026-03-09 | Closed day | L032 |
| 4 | 2026-03-10 | Tutor overlap | L033 · L034 |

## Context

- Endpoint and rule codes: `specs/2026-09-27-violation-report/requirements.md`.
- Page pattern: `specs/2026-09-30-inline-page-loading/`, `specs/2026-09-30-page-routes/`.
