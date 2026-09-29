# Phase 16 (+17): React Today view — Plan

Tasks in order. The whole phase is one commit at the end.

1. **Scaffold** (`web/`)
   - `npm create vite@latest web -- --template react-ts`, then `npm install`.
   - `npm install -D vitest`. Scripts: `dev`, `build`, `test` (`vitest run`).
   - Remove the template's demo content and assets.
   - `vite.config.ts`: `server.proxy['/api'] = 'http://localhost:5238'`.
   - Check that `.gitignore` covers `web/node_modules` and `web/dist` (it has `node_modules/` and `dist/`).

2. **API types and fetch** (`web/src/api.ts`)
   - Types for the day, session, attendee, change, room and tutor views, as in the JSON.
   - `fetchDay(date?: string): Promise<ScheduleDay>`. A network error or a non-2xx status throws an error with the status.

3. **Layout** (`web/src/layout.ts`)
   - `OPENING = { opens: '09:00', closes: '21:30', stepMin: 15 }`, with a comment pointing at `BookingPolicy` in `appsettings.json`.
   - `minutesOf(localIso)`: minutes since midnight, read from the `HH:mm` of the string.
   - `layoutDay(sessions, rooms, window)`: the column from the room order, the row start and span from the minutes, and lanes: within one room, sessions whose times overlap share a group, and each gets a lane index and the group's lane count.
   - `web/src/layout.test.ts`: the cases in `requirements.md`.

4. **Page** (`web/src/App.tsx`, small components in the same folder)
   - State: the date from `?date=` (or none), the last loaded day, and an error.
   - Header: the date, `now`, the three buttons (keeping `?date=` in the URL with `history.replaceState`), and the tutor load line.
   - Grid: a CSS grid with the gutter, the room columns and the 15-minute rows. Cards placed by `layoutDay`. The now line when the date is the API's today.
   - Card: time, tutor, students with lesson IDs, and the struck, faded, outlined, `⚑` and badge states, with their tooltips.
   - The empty-day and API-error messages.

5. **Styling** (`web/src/App.css`)
   - The grid, the tutor colours, the states, and light and dark.
   - Check at 1280 × 800 that the whole day fits with no horizontal scroll.

6. **Run it**
   - The API on a freshly seeded dev DB, then `npm run dev`. Go through the table in `requirements.md`.
   - Cancel L020 by `family` with curl, then reload.
   - Stop the API and reload: the error message.
   - Capture a screenshot of the pinned day to `docs/today-view.png` if the browser tool is available.
   - Reseed the dev DB at the end.

7. **Build and test**
   - `npm test` and `npm run build` in `web/`.
   - `dotnet build` and `dotnet test` still pass.

8. **Docs in the same commit** (`requirements.md` "Docs")
   - Ask the author for the real extra time before editing §4.

9. **Commit**
   - `feat: show the day as a room-by-time grid in a React page`. The body says why: the owner still could not see today on a screen, and this is the cheapest read over a model that now holds. Cancelled, past, now, flagged history and changes after the cut-off are all shown, because the JSON already carries them. Phase 17 is merged in. §4 is corrected in the same commit, because the stretch was built after the time box.
