# Phase 23: Routes for the room grid and the tutor day — Plan

The tasks run in order, and the whole phase is one commit at the end. All paths are under `web/`.

1. **Dependency**: `npm install react-router` (v8, the current release).

2. **Router**
   - `src/main.tsx`: wrap `<App />` in `<BrowserRouter>`.
   - `src/App.tsx`: only the route table.
     - `/rooms` → `DayPage`
     - `/tutors/:tutorId` → `TutorDayPage`
     - `/` → `LegacyRedirect`. It sends `?tutor=T1&date=D` to `/tutors/T1?date=D`, and anything else to `/rooms`, keeping `date` when it is present. It uses `<Navigate replace>`.
     - `*` → `<Navigate to="/rooms" replace />`.

3. **Date in the query**: new `src/useDate.ts`.
   - `useDate()` returns `[date, goToDate]`. `date` is `?date=` or `null`.
   - `goToDate(date | null)` sets `date`, or removes it for `null`, with `{ replace: true }`.

4. **Pages**
   - `src/DayPage.tsx`:
     - No props. Uses `useDate()` and hands `goToDate` to `PageHeader` and `DayGrid`.
     - The tutor line links become `<Link to={`/tutors/${id}?date=${day.date}`}>`.
   - `src/TutorDayPage.tsx`:
     - No props. `tutorId` comes from `useParams()`, `date` from `useDate()`.
     - Both "← All rooms" links become `<Link to="/rooms?date=…">`. On a 404 the link uses the date asked for, if any.
     - The stale-sheet guard and the `useLoad` key stay as they are.
   - Delete `src/place.ts`.

5. **Docs**: `README.md` gives the new URLs for the grid (`/rooms?date=2026-03-04`) and a tutor's day (`/tutors/T1?date=2026-03-06`).

6. **Build, lint, test, run** (see `validation.md`), then **commit** once the author asks:
   `feat: give the room grid and the tutor day their own routes`.
