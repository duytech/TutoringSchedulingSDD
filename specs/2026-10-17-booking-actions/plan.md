# Phase 28: Booking actions in the web — Plan

The tasks run in order, on their own branch, and the whole phase is one commit at the end. Each step's tests are written with its code, not at the end.

1. **`GET /api/students`**
   - `IStudentRepository.ListAsync(ct)` and its `StudentRepository` implementation (no tracking).
   - `Application/Students/GetStudentsHandler.cs` with `StudentView(Guid Id, string Name)`, ordered by `Name`, then `Id`. Register it where `GetRoomsHandler` is registered.
   - `Api/Endpoints/StudentEndpoints.cs` with `MapStudentEndpoints`, called next to `MapRoomEndpoints`. OpenAPI metadata as for rooms.
   - `ReferenceListEndpointTests`: the 6 students in name order. `ApiCalls` gets a `GetStudentsAsync` if the other lists have one.
   - A request in `BrightPath.Api.http`. `dotnet test`.

2. **Component test setup** (in `web/`)
   - `npm i -D jsdom @testing-library/react @testing-library/user-event @testing-library/jest-dom`.
   - `vite.config.ts`: `test: { include: ['src/**/*.test.{ts,tsx}'], setupFiles: ['src/testSetup.ts'] }`. The default environment stays Node.
   - `src/testSetup.ts`: `import '@testing-library/jest-dom/vitest'` and `afterEach(cleanup)`.
   - `src/fakeFetch.ts`: `fakeFetch(routes)` stubs `fetch`, answers `{ method, url } → { status, body }`, and returns the requests it saw. Unknown requests fail the test with their method and URL.
   - `npm test`: the existing tests still pass.

3. **API client** (`web/src/api.ts`)
   - Types `ChangedBy`, `Student`, `CreateSessionRequest`, `MoveSessionRequest`, `CancelAttendeeRequest`, `WriteResult`.
   - `fetchStudents`.
   - A private `postJson(url, body): Promise<WriteResult>`: 2xx → `ok`; 400 with `errors` → `invalid`; 409 with `conflicts` → `conflict`; 404 → `not-found`; a network or gateway error → throw `ApiError('Cannot reach the API.', null)`; anything else → throw `ApiError` with the status.
   - `createSession`, `moveSession`, `cancelAttendee` on top of it, with `encodeURIComponent` on the ids, as `fetchTutorDay` does.

4. **Helpers and their tests**
   - `src/bookingForm.ts`: `toStartsAt`, `timeOptions`, `moveFormFrom`, `isUnchangedMove`.
   - `src/bookingForm.test.ts`: the offset comes from `now` (try a `+07:00` and a `Z` now); `timeOptions` starts at `09:00`, steps by 30 and stays inside `OPENING`; `moveFormFrom` on a 90-minute session; `isUnchangedMove` true when nothing changed, false when any one of date, time, room or length changes.
   - `src/labels.ts`: the three new `ruleLabel` codes and `cancelOutcomeLine`. Tests in `labels.test.ts`: chargeable and late, neither, and the last attendee (session cancelled too).

5. **Components, each with its test** (in `src/components/<Name>/`: `<Name>.tsx`, `<Name>.css`, `<Name>.test.tsx`)
   - `ConflictList`: copy the few pill rules of `.violation__rule` into `ConflictList.css` under its own class names (`.conflicts`, `.conflict__rule`). No shared class across component files.
   - `BookingPanel`: `<aside>`, title, `✕`, Escape to close.
   - `MoveSessionForm`, then `SessionDetails` (with the inline cancel form), then `CreateSessionForm`.
   - Each form keeps its own fields, `sending`, and the last `WriteResult` or `ApiError` in local state. A success is handed up through a callback (`onBooked(session)`, `onMoved(session)`, `onCancelled(session, attendeeId)`).
   - Each test follows its row of the "Component tests" table in `requirements.md`. Find elements by role and label.

6. **Page wiring, with its tests**
   - `PageHeader`: optional `actions` prop, rendered before the day arrows.
   - `SessionCard`: optional `onSelect` and `selected`; the time label becomes a `<button className="card__open">` when `onSelect` is set; `stopPropagation` on the `moved →` button; `card--selected` in `SessionCard.css`. `SessionCard.test.tsx`.
   - `DayGrid`: passes `onSelect` and `selectedSessionId` through to the cards.
   - `DayPage`: fetch students with rooms and tutors; the panel state; `reloadKey`; the after-write moves from `requirements.md` (go to the date, open the session); the grid and the panel side by side in `DayPage.css`, stacked at 640px or narrower. `DayPage.test.tsx` in a `MemoryRouter`.

7. **End-to-end tests**
   - `npm i -D @playwright/test`, then `npx playwright install chromium`.
   - `playwright.config.ts` and the `e2e` script, as in `requirements.md`. Add `playwright.config.ts` and `e2e/**/*.ts` to `tsconfig.node.json`'s `include`, and `playwright-report` and `test-results` to `.gitignore` and to ESLint's ignores.
   - `e2e/rooms.ts` with the shared steps, and the five spec files from the "End-to-end tests" table.
   - Run them: `docker compose down`, `docker compose up -d --build`, `scripts/smoke-test.sh`, `npm run e2e`.

8. **CI**: in the `container` job, after the smoke test: `actions/setup-node` (Node 22, npm cache on `web/package-lock.json`), `npm ci`, `npx playwright install --with-deps chromium`, `npm run e2e` (all in `web/`), and `actions/upload-artifact` for `web/playwright-report/` and `web/test-results/` when it fails.

9. **Docs**: `DECISIONS.md` §3 (students row) and §4, the README parts (the walkthrough and the test commands), the roadmap status line.

10. **Build, lint, test** (see `validation.md`), the two checks by eye, then **commit** once the author asks:
    `feat(web): book, move and cancel sessions from the room grid`.
