# Phase 23: Routes for the room grid and the tutor day — Requirements

Roadmap: not on the roadmap. A web-only change that comes after phase 22. It is one atomic commit.

## Goal

Phase 22 split the screens into `DayPage` and `TutorDayPage`, but both still live on one URL: `?tutor=` decides which page shows. The navigation is hand-rolled (`place.ts`, `goTo`, `linkTo` in `App.tsx`), and every change uses `replaceState`, so the browser's Back button leaves the app.

After this phase each page has its own route:

| URL | Page |
|---|---|
| `/rooms` | The room grid for the API's today |
| `/rooms?date=2026-03-06` | The room grid for that day |
| `/tutors/T1` | T1's day, for the API's today |
| `/tutors/T1?date=2026-03-06` | T1's day on that date |
| `/` | Redirects to `/rooms` |
| `/?tutor=T1&date=2026-03-06` (old link) | Redirects to `/tutors/T1?date=2026-03-06`. Without `tutor`, to `/rooms?date=…` |
| Any other path | Redirects to `/rooms` |

## In scope

- Adding `react-router` and a route table in `App.tsx`.
- The pages read the tutor from the path and the date from the query, and no longer take props from `App`.
- Removing `place.ts`.
- The README's URLs.

## Out of scope

- Any change to the API, `api.ts`, `useLoad`, the page layout or the CSS.
- A date in the path (`/rooms/2026-03-06`).
- A server-side fallback for production hosting. Only the Vite dev and preview servers are used, and both already serve `index.html` for any path.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| URL shape | Page in the path, date in the query. | The page is what the URL names. The date is optional, and leaving it out means today, as before. |
| Router | `react-router` v8 (the current release), declarative mode (`BrowserRouter`, `Routes`, `Link`, `useParams`, `useSearchParams`). | Standard. It replaces the hand-rolled URL code instead of adding to it. |
| History | Opening a page (a tutor link, "← All rooms") **pushes**. Changing the date (← / → / Today, a "moved to" button) **replaces**. | Back returns to the page you came from, without walking back through every day you stepped through. |
| Old links | `/?tutor=…&date=…` redirects to the new route, with replace. | Links already shared, and the README, keep working. |
