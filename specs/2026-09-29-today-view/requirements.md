# Phase 16 (+17): React Today view — Requirements

Roadmap: below the ✂️ cut line. Phase 16 (est. 20 min), with phase 17, the changes badge (est. 8 min), merged into it. One atomic commit.

## Goal

The owner opens the laptop and sees the day: one page with a **room × time grid**, cancelled lessons struck through, what has already happened faded, where "now" is, and which lessons changed after the tutor was told. This is feature 2 (Today board) from `DECISIONS.md` §2, built only as a read over `GET /api/schedule`. It closes the gap §2 names: "the owner cannot see today on a screen yet".

## In scope

- A Vite + React + TypeScript app in `web/`, with one page.
- A pure layout function (sessions → grid positions), tested with Vitest.
- Docs in the same commit: `DECISIONS.md` §2 and §4, `specs/roadmap.md`, `README.md`, `specs/tech-stack.md`.

## Out of scope

- Booking or cancelling from the page. The page only reads. Writes stay in the API, Scalar and `.http`.
- The tutor day sheet (phase 19) and move (phase 18).
- Component tests, browser end-to-end tests, and a production build served by the API.
- Login.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| §4 says the stretch was not built | The phase 16 commit updates §4, the roadmap status and the README: phases 16 and 17 were built **after** the time box, with the real extra time. | Definition of done: when a decision changes, `DECISIONS.md` changes in the same commit. §4 must stay true. |
| Date | The API's today (pinned 03-06) by default, with previous day, next day and "Today" buttons. The date is kept in the URL (`?date=`). | The API already takes `?date=`. The owner can look at 03-03 (L005) or 03-04 (the pair and L008) without a new endpoint. |
| What the grid shows | Cancelled struck through. Past faded, in progress outlined, and a "now" line. A flag on legacy-violation rows. The **after-cutoff badge** (phase 17, merged in). | All of it is already in the schedule JSON (`cancelled`, `state`, `now`, `legacyViolation`, `changedAfterCutoff`). |
| Tests | Vitest on the pure layout function only. | The layout is the only logic in the page. Everything else is markup over the JSON. |

## The page

One screen at laptop width (about 1280 × 800), with no horizontal scroll.

### Header

- The date as `Friday 6 March 2026`, and `now` as `10:00` when the date is today.
- Buttons: `←` previous day, `Today`, `→` next day. `Today` loads `/api/schedule` with no date, so it follows the API's clock (`Clock:Now`), not the browser's.
- One line of tutor loads for the day, counting active sessions only: `T1 Ngoc Anh 7 · T2 Pham Duc 2 · T3 Le Thu 1`.

### Grid

- **Columns:** a time gutter, then one column per room, in the order the API gives (R1–R6). Every room is shown, even an empty one.
- **Rows:** 15-minute rows from **09:00 to 21:30**, the centre's opening hours (`BookingPolicy` defaults). The hours are one constant in `web/src/layout.ts`, with a comment pointing at `appsettings.json`. Labels on each hour and half hour.
- **A session card** sits in its room's column, from its start row, spanning `durationMin / 15` rows. It shows:
  - the time (`10:30–12:00`) and the tutor (`T3 Le Thu`);
  - each student on its own line, with the lesson ID small (`L020`). A cancelled attendee is struck through on its own, so a pair with one cancelled student reads right;
  - the tutor's colour (one colour per tutor, from a fixed palette in the order of the `tutors` list).
- **Two sessions in one room at the same time** (a cancelled session and its rebooking, or a flagged overlap) sit side by side in lanes, each half as wide. They never hide each other.

### States

| Data | Shown as |
|---|---|
| `cancelled` session | The whole card struck through and greyed, still in its place |
| `state` = `past` | Faded |
| `state` = `in-progress` | A thicker outline |
| `now`, when the shown date is the API's today | A red horizontal line across the grid at that time |
| `legacyViolation` on the session or an attendee | A `⚑` marker. Its tooltip: `Loaded from the export; breaks a centre rule. See /api/reports/violations.` |
| `changedAfterCutoff` (phase 17) | A badge `changed after tutor was told`. Its tooltip lists the late changes (kind, who, local time) |

### Other cases

- **No sessions that day:** the empty grid, plus `No sessions on this day.`
- **API unreachable, or an error status:** `Cannot reach the API. Is it running (dotnet run --project src/BrightPath.Api)?` and the status if there is one.
- **Loading:** the previous grid stays until the new day arrives, so the page does not flash.

## Expected on the export (clock 2026-03-06 10:00)

| Date | What the page shows |
|---|---|
| 03-06 (default) | R1 holds 7 cards (T1). L018 and L019 are faded (past). The now line is at 10:00. R4–R6 are empty. Tutor line: `T1 Ngoc Anh 7 · T2 Pham Duc 2 · T3 Le Thu 1` |
| 03-03 | L005 (R2, 14:00) struck through, with the after-cutoff badge |
| 03-04 | L009 + L010 are one card with two students. L008 (R2, 09:00) has `⚑` |
| 03-05 | L017 struck through, with the badge. L015 (no-show) is not struck through |
| 03-10 | L034 (R2, 09:00) has `⚑` |
| 03-12 | Empty grid and `No sessions on this day.` |
| 03-06, after a family cancel of L020 through the API | Reload: L020 struck through, with the badge |

## Technical notes

- **Scaffold:** `npm create vite@latest web -- --template react-ts`. Add `vitest` as a dev dependency. No UI library, no router, no state library, no date library (`Intl.DateTimeFormat` with `timeZone: 'Asia/Ho_Chi_Minh'`).
- **Proxy:** `vite.config.ts` proxies `/api` to `http://localhost:5238`, so no CORS setup is needed (`specs/tech-stack.md`).
- **Types:** `web/src/api.ts` has hand-written TypeScript types for `ScheduleDayView` and its parts, matching the JSON, and one `fetchDay(date?)` function.
- **Times:** the API sends local times with `+07:00`. The grid position is read from the `HH:mm` of that local string, not converted through the browser's zone, so a reviewer in another time zone sees the same grid.
- **Layout function** (`web/src/layout.ts`, pure): `layoutDay(sessions, rooms, window)` returns, for each session, `{ column, rowStart, rowSpan, lane, lanes }`. Tested for: the row of a 09:00 and a 10:30 start, the span of 60 and 90 minutes, the column from the room order, two overlapping sessions in one room getting lanes 0 and 1 of 2, and a later session in the same room going back to one lane.
- **Styling:** plain CSS (`web/src/App.css`), a CSS grid. Light and dark via `prefers-color-scheme`.
- **Scripts:** `npm run dev`, `npm test` (Vitest, run once), `npm run build` (type check and build).

## Docs in the same commit

- `DECISIONS.md` §2 "What I leave broken": the owner can now see a day on a screen (read only).
- `DECISIONS.md` §4 "Where it stopped": phases 16 and 17 were built after the time box, with the real extra time (given by the author). "Next week" item 1 becomes the tutor day sheet only.
- `specs/roadmap.md`: the status line says 16 and 17 (merged) were built, and 18 and 19 were not.
- `README.md`: a short "Web" section (prerequisite Node 20+, `npm install`, `npm run dev` with the API running, the URL, `npm test`), and a screenshot of the pinned day if one can be captured.
- `specs/tech-stack.md` Frontend row: plus Vitest for the layout.

## Context

- `DECISIONS.md` §2 (feature 2, what is left broken), §3 (the schedule API row), §4.
- Phase 10 spec: the schedule JSON (`state`, `changedAfterCutoff`, the indexes).
- `specs/tech-stack.md`: Frontend row, `/web/` in the layout.
