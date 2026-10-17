# Phase 28: Booking actions in the web — Validation

The phase can be merged when every item below is true.

## Automated, on the dev machine

- [ ] `dotnet build BrightPath.slnx` succeeds with zero warnings.
- [ ] `dotnet test` passes, with the new students test in `ReferenceListEndpointTests`.
- [ ] In `web/`: `npm run build` succeeds and `npm run lint` reports nothing, `e2e/` and `playwright.config.ts` included.
- [ ] In `web/`: `npm test` passes, with:
  - `bookingForm.test.ts` and the new `labels.test.ts` cases,
  - the seven component tests (`CreateSessionForm`, `MoveSessionForm`, `SessionDetails`, `ConflictList`, `BookingPanel`, `SessionCard`, `DayPage`),
  - `structure.test.ts`, which sees the 9 new component folders.
- [ ] On a fresh stack (`docker compose down`, `docker compose up -d --build`, `scripts/smoke-test.sh`): `npm run e2e` passes, all five spec files.
- [ ] `npm run e2e` run a second time on the same stack fails (the data has changed). This shows the tests really write through the API, and that the README's "fresh stack" note is needed.

## The tests test the right thing

Checked by hand once, not committed:

| # | Break this | Then fails |
|---|---|---|
| 1 | `toStartsAt` drops the offset | `bookingForm.test.ts`, `CreateSessionForm.test.tsx`, and `book.spec.ts` (the API answers 400) |
| 2 | `ConflictList` shows only the first conflict | `CreateSessionForm.test.tsx`, `book.spec.ts` |
| 3 | `cancelledBy` gets a default of `family` | `SessionDetails.test.tsx` (a cancel is sent with no choice made) |
| 4 | `SessionCard` calls `onSelect` from the `moved →` link | `SessionCard.test.tsx` |
| 5 | `DayPage` does not bump `reloadKey` after a cancel | `DayPage.test.tsx`, `cancel.spec.ts` (the card is not struck through) |

## CI

- [ ] The `web` job runs the component tests through `npm test` and passes.
- [ ] The `container` job runs `npm run e2e` after the smoke test and passes.
- [ ] A failing e2e test (tried once on a throwaway commit) uploads the Playwright report as an artifact.

## By eye (Playwright CLI, API and `npm run dev`)

Only what code cannot judge:

| # | Check | Expected |
|---|---|---|
| 1 | Light and dark mode, panel open on a form and on a conflict list | Readable in both, using the `global.css` tokens |
| 2 | Window at 600px wide, panel open | The panel stacks under the grid, no horizontal scroll |

## Endpoint (curl)

| # | Check | Expected |
|---|---|---|
| 1 | Scalar UI / `/openapi/v1.json` | `GetStudents` is listed under `Students` |

## Docs and commit

- [ ] `DECISIONS.md` §3 has the `GET /api/students` row. §4 says the web can book, move and cancel from the room grid.
- [ ] `README.md` has the "Book, move and cancel in the web" part, and says how to run the component and e2e tests.
- [ ] `specs/roadmap.md` status: phase 28 built.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
- [ ] The dev DB is reseeded to the export afterwards.
