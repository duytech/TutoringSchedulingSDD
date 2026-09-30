# Phase 22: Tutor day as its own page — Requirements

Roadmap: not on the roadmap. A web-only refactor that comes after phase 21. It is one atomic commit.

## Goal

`web/src/App.tsx` drives both screens from one place:
- `load()` fetches either the room grid (`fetchDay` + `fetchRooms` + `fetchTutors`) or the tutor sheet (`fetchTutorDay`).
- One `Shown` union holds whichever came back.
- The JSX branches on `shown.kind` and `place.tutor`.

Reading one screen means reading the other one too.

After this phase each screen is its own page component, fetching its own data:

| File | Role |
|---|---|
| `App.tsx` | Keeps the place (`date`, `tutor`) in sync with the URL. Picks `TutorDayPage` when `tutor` is set, otherwise `DayPage`. No fetching. |
| `DayPage.tsx` | The room grid: header with date, "Now" and the tutor line, then `DayGrid`. |
| `TutorDayPage.tsx` | One tutor's day: header with name, date, cutoff line and "← All rooms", then `TutorSheet`. Shows "No tutor …" on a 404. |
| `useLoad.ts` | Shared loading hook: data, error, loading, and the guard that drops a stale response. |
| `PageHeader.tsx` | Shared header layout (title, sub-line, loading marker, ← / Today / → nav) and the "Cannot reach the API" message. |
| `colours.ts` | `tutorColour`, used by both pages. |

## In scope

- Splitting `App.tsx` into the files above.
- Tests and lint stay green.

## Out of scope

- Any change to the URL (`?tutor=T1&date=…` stays), to the API, or to `api.ts`.
- A router library or path-based routes.
- Visual changes: CSS class names and the rendered markup stay the same.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Page boundary | Component pages that each fetch their own data. | Each screen can be read and changed alone. |
| URL | Unchanged, no router. | Old links and the README keep working. Two pages do not need a router. |
| Loading state | `loading` is derived from "the key asked for ≠ the key loaded", not set in `goTo`. | No synchronous `setState` inside an effect (react-hooks v7 lint), and the page cannot forget to set it. |
| While loading | The previous page stays on screen until the new data arrives. | Same as today: the page does not flash. |
| Stale data | `TutorDayPage` only shows a sheet whose `tutorId` matches the tutor asked for. | Switching tutors quickly must never show the wrong tutor. |
