# Phase 25: Violations page — Plan

The tasks run in order, and the whole phase is one commit at the end. All paths are under `web/src/` unless they say otherwise.

1. **API client** (`api.ts`)
   - `ScheduleViolation`: `rule`, `date`, `sessionIds`, `lessonIds`, `message`, the same as `ScheduleViolation` in `BrightPath.Domain`.
   - `ViolationReport`: `{ violations: ScheduleViolation[] }`.
   - `fetchViolations(from?: string, to?: string)`: builds the query with `URLSearchParams`, leaves out a missing date, and calls `getJson`. A 400 comes back as an `ApiError` with status 400.
   - Add the endpoint to the comment at the top of the file.

2. **Pure helpers, with tests**
   - `labels.ts`: `ruleLabel(code: string): string`, a map of the 7 codes in `RuleCodes.All`. Anything else is returned unchanged.
   - `violations.ts`: `groupByDate(violations)` returns `{ date: string; violations: ScheduleViolation[] }[]`, with dates in first-seen order.
   - `labels.test.ts`: one known code, and one unknown code.
   - `violations.test.ts`: items on two dates keep the API order inside each group, and an empty list gives `[]`.

3. **Page** (`ViolationsPage.tsx`)
   - `useRange()`, in the same file: reads `from` and `to` from `useSearchParams`, and returns a setter that writes or deletes each one with `{ replace: true }`.
   - The effect is keyed on `${from}|${to}`. It has the `current` guard, keeps the last report while loading, and records `loadedKey`.
   - An `ApiError` with status 400 is shown as "From must be on or before To.". Any other error goes to `ApiErrorMessage`.
   - Markup:
     - `<main className="page">`.
     - `<header className="top">` with the h1 "Rule breaks", the count line, and "← All rooms" (`<Link to="/rooms">`).
     - The range form: two `<input type="date">` with labels "From" and "To", plus a "Clear" button.
     - Then one `<section>` per date group: an h2 from `longDate`, and an `<ul>` of items.

4. **Routes and link**
   - `App.tsx`: `<Route path="/violations" element={<ViolationsPage />} />`, placed before the `*` catch-all.
   - `DayPage.tsx`: after `TutorLoads`, add ` · <Link to="/violations">Violations</Link>`.

5. **Styles** (`App.css`)
   - `.range`: the inputs in a row, wrapping on a narrow screen.
   - `.violations`: date groups with spacing.
   - `.violation`: the rule label as a small tag next to the message, and the lesson IDs muted underneath.
   - Reuse the existing colour variables. Add no new ones unless they are missing.

6. **Docs**
   - `README.md` "Web: the Today view": one paragraph saying that `/violations` lists every rule break grouped by day, that the dates stay in the URL, and that the room grid links to it.

7. **Build, lint, test, run** (see `validation.md`), then **commit** once the author asks:
   `feat: show the violation report as a page`.
