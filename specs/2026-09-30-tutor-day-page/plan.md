# Phase 22: Tutor day as its own page — Plan

The tasks run in order, and the whole phase is one commit at the end. All paths are under `web/src/`.

1. **Shared pieces**
   - `colours.ts`: move `TUTOR_COLOURS` and `tutorColour` from `App.tsx`.
   - `useLoad.ts`: `useLoad<TData>(key: string, load: () => Promise<TData>)` returns `{ data, error, loading }`.
     - Move in the `current` race flag and the `ApiError` wrapping from `App.tsx`.
     - State is `{ key, data, error }`, and it is only set in the promise callbacks.
     - `loading` is `key !== state.key`.
   - `PageHeader.tsx`:
     - `PageHeader`: props `title`, `date`, `loading`, `onGoToDate`, and `children` for the sub-line. It renders the existing `top` header and its ← / Today / → nav.
     - `ApiErrorMessage`: the "Cannot reach the API" block.
   - `Place`, `Link` and `urlOf` move to `place.ts`, so the pages can import the types without importing `App`.

2. **Pages**
   - `DayPage.tsx`: props `date`, `onGoToDate`, `linkTo`.
     - Loads the day, the rooms and the tutors with `useLoad`.
     - Renders `PageHeader` (long date, "Now", `TutorLoads`), `ApiErrorMessage`, "No sessions on this day." and `DayGrid`.
     - `TutorLoads` moves here from `App.tsx`.
   - `TutorDayPage.tsx`: props `tutor`, `date`, `onGoToDate`, `linkTo`.
     - Loads with `fetchTutorDay`.
     - Renders `PageHeader` (name · date, `cutoffLine`, "← All rooms"), "No tutor …" on a 404 or else `ApiErrorMessage`, and `TutorSheet`.
     - Keeps the `sheet.tutorId === tutor` guard.

3. **App**
   - `App.tsx` keeps the `place` state, `goTo`, `go` and `linkTo`, and renders one of the two pages.
   - `load`, `Shown`, the `loading`/`error`/`shown` state and the header JSX go.

4. **Build, lint, test, run** (see `validation.md`), then **commit** once the author asks:
   `refactor: give the tutor day its own page`.
