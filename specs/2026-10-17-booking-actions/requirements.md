# Phase 28: Booking actions in the web — Requirements

Roadmap: not on the roadmap. It comes after phase 27 (one folder per web component). It is one atomic commit, delivered through a pull request.

## Goal

The API books, moves and cancels (phases 11, 13 and 18), but the web app only reads. A receptionist who wants to book a lesson has to use curl or `BrightPath.Api.http`. After this phase they can do all three from the room grid:

- **Book**: a `+ New session` button opens a form in a side panel.
- **Cancel**: clicking a card opens that session in the panel. Each booked student has a **Cancel**.
- **Move**: the same panel holds a **Move** form, filled in with the session's time, room and length.

The server stays the **only judge of the rules**. The web never re-implements `ScheduleRules` or `BookingPolicy`. It sends the request and shows the answer: every 400, 404 and 409 in plain words, with all the conflicts at once, as the API lists them.

One gap blocks the create form: there is no way to list students. `IStudentRepository` only has `FindManyAsync(ids)`. So the phase also adds a small `GET /api/students`.

## In scope

- `GET /api/students`, with one integration test.
- The four calls in `api.ts`: `fetchStudents`, `createSession`, `moveSession`, `cancelAttendee`.
- Pure form helpers with unit tests.
- Five new components: `BookingPanel`, `CreateSessionForm`, `SessionDetails`, `MoveSessionForm`, `ConflictList`.
- `SessionCard` can be selected, and `DayPage` holds the panel.
- **Component tests** (Vitest, Testing Library, jsdom) for the forms, the panel, the card and the page, with `fetch` faked. They run in `npm test`, so in the CI `web` job.
- **End-to-end tests** (Playwright Test) for the main flows, against the real API and a freshly seeded database (the `docker compose` stack). They run in the CI `container` job.
- Docs in the same commit.

## Out of scope

- Actions on the tutor page (`/tutors/:tutorId`). It stays a read-only sheet for the tutor.
- Drag and drop, and booking by clicking an empty slot of the grid.
- Adding a student to an existing session (no endpoint, `DECISIONS.md` §3).
- Cancelling a whole session in one call. Cancel each student; the last one cancels the session (phase 13).
- Undoing a cancel or a move.
- Checking the rules in the browser.
- Screenshot or visual-regression tests. Light and dark mode and the narrow layout are the only checks left by eye (`validation.md`).
- Component tests for the pages and components that already exist and do not change here (`ViolationsPage`, `TutorSheet`, …).

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Students | Add `GET /api/students`: every student `{ id, name }`, ordered by name, then id. Built like `GET /api/rooms`. | The form needs names to pick from. Collecting them from the attendees of a few days would miss students with no lesson that week. |
| UI | A side panel on the Rooms page. `+ New session` opens the create form. Clicking a card opens that session, with a **Cancel** per booked student and a **Move** form. | The grid stays in sight while you book, so the receptionist can see the free slot they are aiming at. |
| Pages | The Rooms page (`/rooms`) only. | The receptionist books from the room grid. The tutor's page is what the tutor reads. |
| Phasing | One phase, one commit. | Create, move and cancel share the panel, the POST helper and the conflict list. |
| Rules | No rule is checked in the browser. The server's 400 and 409 are shown as they come. | One place for the rules, so the web and the API cannot disagree. |
| Who | `cancelledBy` and `movedBy` are required and have **no default**. | `cancelledBy` decides `chargeable` (only a late family cancel pays). A default would make a wrong charge one click away. |
| Time zone | The form's local date and time get the offset of the API's `now` (`+07:00`). | The API refuses a time without an offset. The browser's zone is not the centre's. |
| Tests | Two layers. Component tests with `fetch` faked check what the UI sends and shows. A few end-to-end tests check that the web and the real API agree. No screenshots. | Component tests are fast, need no database, and cover every branch of a form. Only an end-to-end test proves that a real 409 arrives in the shape the form reads. Every check runs in CI, not by hand. |
| E2E target | The `docker compose` stack (web on `http://localhost:8080`, no DB volume). | Every `up` is a freshly seeded week with the pinned clock, the same stack CI already builds and smoke-tests. No test-only reset endpoint is needed. |

## Backend: `GET /api/students`

- `IStudentRepository.ListAsync(ct)`: every student. `StudentRepository` implements it.
- `Application/Students/GetStudentsHandler.cs`: `StudentView(Guid Id, string Name)`, ordered by `Name`, then `Id`. Same shape as `GetRoomsHandler`.
- `Api/Endpoints/StudentEndpoints.cs`: `MapGroup("/api/students")`, `.WithTags("Students")`, `.WithName("GetStudents")`, `.Produces<IReadOnlyList<StudentView>>()`. Registered like `MapRoomEndpoints`.
- Integration test in `ReferenceListEndpointTests`: the 6 students of the export, in name order (`Bui An Nhien` first, `Vu Ha My` last).
- A request in `BrightPath.Api.http`.

## Web: API client (`api.ts`)

New types:

```ts
export type ChangedBy = 'family' | 'tutor' | 'centre'

export interface Student { id: string; name: string }

export interface CreateSessionRequest {
  tutorId: string
  roomId: string
  startsAt: string // "2026-03-07T13:00:00+07:00"
  durationMin: number
  studentIds: string[]
}

export interface MoveSessionRequest {
  startsAt: string
  roomId: string
  durationMin: number
  movedBy: ChangedBy
  note: string | null
}

export interface CancelAttendeeRequest { cancelledBy: ChangedBy; note: string | null }
```

The answers a form must show are returned, not thrown:

```ts
export type WriteResult =
  | { kind: 'ok'; session: Session }                        // 200 or 201
  | { kind: 'invalid'; errors: Record<string, string[]> }   // 400 ValidationProblem
  | { kind: 'conflict'; conflicts: ScheduleViolation[] }    // 409 with `conflicts`
  | { kind: 'not-found' }                                   // 404
```

- `fetchStudents(): Promise<Student[]>` uses `getJson`.
- `createSession`, `moveSession(sessionId, …)` and `cancelAttendee(sessionId, attendeeId, …)` share one private `postJson`.
- A network error or a gateway error still throws `ApiError('Cannot reach the API.')`, as `getJson` does. Any other status (500, an unexpected 4xx) throws `ApiError` with that status.
- The `errors` keys are the API's camelCase field names (`startsAt`, `studentIds`, …), so each form can put a message under its field. A key the form does not know is shown above the buttons.

## Web: form helpers (pure, unit-tested)

In `src/bookingForm.ts`:

| Function | Does |
|---|---|
| `toStartsAt(date, time, now)` | `("2026-03-07", "13:00", "2026-03-06T10:00:00+07:00")` → `"2026-03-07T13:00:00+07:00"`. The offset is the last 6 characters of `now`. |
| `timeOptions()` | Start times from `OPENING.opens` up to `OPENING.closes`, every 30 minutes (`09:00`, `09:30`, …). Every seeded lesson starts on one of them. |
| `moveFormFrom(session)` | The Move form filled in from the session: date, time, room, length. `movedBy` empty, note empty. |
| `isUnchangedMove(form, session)` | True when date, time, room and length are all the session's own. The Move button is then disabled. The API's 400 `Nothing to move` stays the backstop. |

In `src/labels.ts`:

- `ruleLabel` learns `in-the-past` (`In the past`), `already-started` (`Already started`) and `already-cancelled` (`Already cancelled`).
- `cancelOutcomeLine(session, attendeeId)`: what a cancel did, read from the returned session: `Bui An Nhien cancelled · chargeable · after the tutor was told`. The `chargeable` part only when it is, the cut-off part only when the new change has `afterCutoff`. When the session went too, `· the session is cancelled` is added.

## Web: components

Each one is a folder `<Name>/` with `<Name>.tsx` and `<Name>.css` under `src/components/` (`CLAUDE.md`, checked by `structure.test.ts`).

| Component | Shows |
|---|---|
| `BookingPanel` | An `<aside>` beside the grid, with a title and a `✕` close button. Escape closes it too. At 640px or narrower it stacks under the grid. |
| `CreateSessionForm` | Date (default: the shown date), start time, length (60 / 90), room, tutor (`T1 Ngoc Anh`), student, and an optional second student for an exam pair. **Book** is disabled while the request runs. |
| `SessionDetails` | Time, tutor and room, then each attendee with their status, and `chargeable` when set. Each `booked` attendee has a **Cancel** that opens a small form: `cancelledBy` (required), note, **Confirm cancel**. Under the attendees, `MoveSessionForm`. |
| `MoveSessionForm` | Date, start time, room, length (filled in by `moveFormFrom`), `movedBy` (required), note. **Move** is disabled while the request runs or while `isUnchangedMove`. |
| `ConflictList` | The 409's conflicts, one per line: the rule pill (the same look as `ViolationList`) and the API's message. |

- For a session that is cancelled, or that has started (`isCancelled`, `sessionState` ≠ `upcoming`), `SessionDetails` shows no Cancel and no Move. One line says why: `Cancelled — nothing to change.` or `Started at 09:00 — it can no longer be moved or cancelled.` This only hides buttons that would surely fail; the API still refuses (409 `already-started`, `already-cancelled`).
- A 400 shows each message under its field. A 404 shows `This session no longer exists.` A thrown `ApiError` shows the `ApiErrorMessage` text inside the panel. In every case the form keeps what was typed.
- `note` has `maxLength={500}`, the API's limit.
- Every field has a `<label>`, and every button has visible text, so the tests find them by role and label (`getByLabelText('Room')`, `getByRole('button', { name: 'Book' })`), not by class name.

## Web: page wiring

`DayPage`:

- Loads `fetchStudents()` with the rooms and tutors.
- Holds the panel: `null`, `{ kind: 'create' }` or `{ kind: 'session', sessionId }`. It is local state, not in the URL. Moving to another day closes a session panel whose session is not on that day.
- `+ New session` sits in the header, next to the day arrows. `PageHeader` gets an optional `actions` prop for it, so the tutor page is unchanged.
- A `reloadKey` in the load effect refreshes the day after a write.

After a write:

| Action | Then |
|---|---|
| Book | Go to the new session's date and open its panel. |
| Move | Go to the new session's date (if it changed) and open the new session. The old card shows `moved → …` (phase 18). |
| Cancel | Stay on the session. Show `cancelOutcomeLine` at the top of the panel, above the refreshed attendees. |

`SessionCard`:

- Gains optional `onSelect` and `selected`. With `onSelect`, a click on the card opens it, and the time label is a `<button className="card__open">` so the keyboard can reach it. The `moved →` link stops the click from reaching the card.
- `selected` adds `card--selected` (an outline in the tutor's colour).
- `TutorSheet` passes neither, so the tutor page's cards look and act as before.

## Tests

### Component tests (Vitest + Testing Library + jsdom)

- New devDependencies: `jsdom`, `@testing-library/react`, `@testing-library/user-event`, `@testing-library/jest-dom`.
- The existing pure tests keep running in Node. A component test opts in to jsdom with `// @vitest-environment jsdom` on its first line.
- `src/testSetup.ts` (Vitest `setupFiles`) loads the jest-dom matchers and runs `cleanup` after each test.
- `src/fakeFetch.ts`: a small test helper over `vi.stubGlobal('fetch', …)`. It answers by method and URL with a status and a JSON body, and keeps the requests it saw, so a test can read the last request's URL and body.
- Each test sits next to its component: `src/components/<Name>/<Name>.test.tsx`. `structure.test.ts` still passes: it only asks that `<Name>.tsx` and `<Name>.css` are there.

| File | Checks |
|---|---|
| `CreateSessionForm.test.tsx` | **Book** sends `POST /api/sessions` with `tutorId`, `roomId`, `startsAt` with `+07:00`, `durationMin`, and one or two `studentIds`. A 201 calls `onBooked` with the session. A 409 lists every conflict (label and message) and keeps the values. A 400 shows the `studentIds` message under the student field. **Book** is disabled while the request runs. A thrown `ApiError` shows the "Cannot reach the API" message. |
| `MoveSessionForm.test.tsx` | Filled in from the session. **Move** is disabled until the date, time, room or length changes. It sends `POST /api/sessions/{sessionId}/move` with the form's values and `movedBy`. A 409 lists the conflicts. A 201 calls `onMoved`. |
| `SessionDetails.test.tsx` | A booked attendee has **Cancel**; a cancelled one does not. **Confirm cancel** without `cancelledBy` sends nothing. With it, it sends `POST /api/sessions/{sessionId}/attendees/{attendeeId}/cancel` with `cancelledBy` and the note. A started session and a cancelled session show their line and no Cancel or Move. A 404 shows `This session no longer exists.` |
| `ConflictList.test.tsx` | One line per conflict with its rule label. A code the web does not know is shown as it is. |
| `BookingPanel.test.tsx` | `✕` and Escape both call `onClose`. |
| `SessionCard.test.tsx` | Without `onSelect`: no `card__open` button, and a click does nothing (the tutor page). With it: the time button and a click on the card call `onSelect`; a click on the `moved →` link does not. `selected` adds `card--selected`. |
| `DayPage.test.tsx` | In a `MemoryRouter` at `/rooms?date=2026-03-06`, with the five GETs faked. `+ New session` opens the create form. A click on a card opens its session. After a booking on 03-07, the URL has `?date=2026-03-07` and the new session's panel is open. After a cancel, `GET /api/sessions` is called again. |

The pure helpers (`bookingForm.ts`, `labels.ts`) keep plain unit tests, as before.

### End-to-end tests (Playwright Test)

- New devDependency: `@playwright/test`. Chromium only.
- `web/playwright.config.ts`: `testDir: 'e2e'`, `baseURL` from `E2E_BASE_URL` (default `http://localhost:8080`), one worker, no retries, trace on the first failure. No `webServer`: the stack is started outside, so the test cannot reuse an old, already-changed database by mistake.
- `npm run e2e` runs `playwright test`. Vitest only includes `src/**/*.test.{ts,tsx}`, so it never picks up `e2e/`.
- `e2e/` and `playwright.config.ts` are in `tsconfig.node.json`, so ESLint type-checks them.
- The tests change the data, so they need a **fresh stack**: `docker compose down` then `docker compose up -d --build`. Each test uses its own sessions and slots, so the tests do not depend on each other's order, but a second run on the same stack fails.

| File | Scenario |
|---|---|
| `e2e/book.spec.ts` | Book T2, R4, 2026-03-07 13:00, 60, Vu Ha My: the page goes to 03-07, a card is in R4, its panel is open. Book it again: Room overlap, Tutor overlap and Student overlap are listed. |
| `e2e/conflicts.spec.ts` | Book T1, R4, 2026-03-06 14:30, Do Van Kien: only Tutor load, and the message names 8 sessions. |
| `e2e/cancel.spec.ts` | On 03-06, open L020 and cancel Bui An Nhien as `family`: the outcome line says chargeable and after the tutor was told, and the card is cancelled. `GET /api/sessions?date=2026-03-06` (through `page.request`) agrees: the attendee is `chargeable`. Open L018: no Cancel and no Move. |
| `e2e/move.spec.ts` | On 03-07, open L028 and move it to 14:00 as `family`: the new session's panel is open, and the old card shows `moved → 14:00 R3`. On 03-06, move L021 to 14:30 R4: only Tutor load. |
| `e2e/tutor.spec.ts` | `/tutors/T1?date=2026-03-06`: no `+ New session`, and a click on a card opens no panel. |

### In CI (`.github/workflows/ci.yml`)

- The `web` job is unchanged: `npm test` now runs the component tests too.
- The `container` job, after `scripts/smoke-test.sh`: set up Node, `npm ci` in `web/`, `npx playwright install --with-deps chromium`, `npm run e2e`. On failure, upload `web/playwright-report/` and `web/test-results/` as an artifact. The stack it tests is the fresh one the job just started.

## Expected results on this export

The clock is at 2026-03-06 10:00 (+07:00), on a freshly seeded database. The rows repeat rows of the phase 11, 13 and 18 tables, so the web and the API must agree. The last column says which test checks the row.

| # | In the web | Result | Checked by |
|---|---|---|---|
| 1 | `+ New session`: 2026-03-07 13:00, 60, R4, T2, Vu Ha My | Booked. The page goes to 03-07, the card is in R4 and its panel is open | `book.spec.ts`, `DayPage.test.tsx` |
| 2 | The same booking again | Conflict list: Room overlap, Tutor overlap, Student overlap. The form keeps its values | `book.spec.ts`, `CreateSessionForm.test.tsx` |
| 3 | 2026-03-06 14:30, 60, R4, T1, Do Van Kien | Only Tutor load. The message names 8 sessions, limit 6 | `conflicts.spec.ts` |
| 4 | 03-06, click L020 (10:30 R3), Cancel Bui An Nhien, `family` | `Bui An Nhien cancelled · chargeable · after the tutor was told · the session is cancelled`. The card is struck through, with the badge | `cancel.spec.ts`, `labels.test.ts` |
| 5 | 03-06, click L018 (09:00 R1) | No Cancel, no Move. `Started at 09:00 — it can no longer be moved or cancelled.` | `cancel.spec.ts`, `SessionDetails.test.tsx` |
| 6 | 03-07, click L028 (09:00 R3), Move to 03-07 14:00, `family` | The new session at 14:00 R3 is open. The old card shows `moved → 14:00 R3` | `move.spec.ts` |
| 7 | 03-06, click L021 (11:30 R1), Move to 14:30 R4, `tutor` | Only Tutor load: T1 still has 7 that day | `move.spec.ts` |
| 8 | Any Move form left as filled in | **Move** is disabled | `MoveSessionForm.test.tsx` |
| 9 | **Confirm cancel** with no `cancelledBy` | Nothing is sent (required field) | `SessionDetails.test.tsx` |
| 10 | The API cannot be reached, then **Book** | The "Cannot reach the API" message in the panel. The form keeps its values | `CreateSessionForm.test.tsx` |
| 11 | `/tutors/T1?date=2026-03-06` | As before: no panel, no `+ New session`, cards do nothing on click | `tutor.spec.ts`, `SessionCard.test.tsx` |

The other create rules (closed day, outside hours, in the past, too many attendees) already have API tests (phases 11 and 12). The web shows them through the same `ConflictList`, so they get no web test of their own.

## Docs

- `DECISIONS.md` §3 API table: a `GET /api/students` row.
- `DECISIONS.md` §4: the web can now book, move and cancel from the room grid; the tutor page stays read-only.
- `README.md`: a short "Book, move and cancel in the web" part under the web app, walking through rows 1, 4 and 6. Under the test commands: `npm test` now includes component tests, and how to run `npm run e2e` on a fresh `docker compose` stack.
- `specs/roadmap.md` status: phase 28 built.

## Context

- Phase 11 (create, the 409 shape), phase 13 (cancel, `chargeable`, `afterCutoff`), phase 18 (move, `movedTo`, the `moved →` label).
- Phase 27 (the component folder rule, `structure.test.ts`).
