# Phase 19: Tutor day — Plan

Roadmap: below the ✂️ cut line, stretch phase 19 (est. 8 min for the endpoint, more with the page). One atomic commit.

This phase has one file: the goal, the decisions, the task list and the verification.

## Goal

`GET /api/tutors/{id}/day?date=` gives **one tutor's authoritative day**: every session they teach that date, cancelled and moved ones included, plus a short list of **what changed after they were told** (the 16:00 cut-off the day before, Q6). A tutor page in `web/` shows it.

This is feature 3 in `DECISIONS.md` §2, the "Tutor day sheet". It answers "which message is real?" with one page per tutor per day that is always read from the schedule, never typed by hand. It does not push anything to the tutor: notifications (feature 4) stay out.

## In scope

- The endpoint, in a new `/api/tutors` group.
- A pure builder for the sheet, reusing `ScheduleDay.View` for each session.
- A tutor page in the web app (`?tutor=T1&date=…`), linked from the Today view's tutor load line.
- Unit tests, integration tests on the phase 12 host, Vitest for any new pure helper, and a check in the browser with the Playwright CLI.
- Docs in the same commit.

## Out of scope

- Sending the sheet to the tutor (feature 4), and a "day sent to the tutor" record (Q6 stays on the 16:00 default).
- A tutor login. Anyone can open any tutor's sheet, as with the Today view.
- A week or date-range view. One tutor, one date.
- Print styling beyond what the page already does.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Scope | API **and** a web page. | The sheet is for a tutor to read. JSON alone does not fix "which message is real?". |
| Shape | The day's sessions in the schedule's session shape, **plus** a flat `changesAfterCutoff` list. | The tutor should see "what changed since I was told" at the top, without opening each session. The per-session `changes` stay as they are. |
| Which sessions | Every session of that tutor whose **local start date** is the date: active, cancelled, moved away (with `movedTo`) and flagged (`legacyViolation`). | Same rule as `GET /api/schedule`. A struck-through lesson is exactly what the tutor must not drive in for. |
| Moved in from another day | Shown as a normal session on its new date. Its `moved` change (`from 2026-03-07 14:00 in R3`) says where it came from. | No new link column: the change note already says it. |
| Unknown tutor | **404** `ProblemDetails`. A known tutor with nothing that day is **200** with empty lists. | A day off is an answer, a typo is not. |

## Response

`GET /api/tutors/T3/day?date=2026-03-05`

```json
{
  "tutorId": "T3",
  "tutorName": "Le Thu",
  "date": "2026-03-05",
  "now": "2026-03-06T10:00:00+07:00",
  "cutoff": "2026-03-04T16:00:00+07:00",
  "final": true,
  "sessions": [ /* ScheduleSessionView, ordered by start, then room */ ],
  "changesAfterCutoff": [
    {
      "sessionId": "…",
      "sessionStartsAt": "2026-03-05T16:00:00+07:00",
      "roomId": "R3",
      "kind": "cancelled",
      "attendeeId": "…",
      "studentName": "Do Van Kien",
      "changedAt": "2026-03-05T14:40:00+07:00",
      "changedBy": "tutor",
      "note": "tutor sick"
    }
  ]
}
```

| Field | Meaning |
|---|---|
| `date` | The `date` query, or today on the pinned clock. A bad date is 400, as in the schedule. |
| `cutoff` | 16:00 local on the calendar day before `date` (`BookingPolicyOptions.CutoffLocalTime`), even when that day is a Monday. |
| `final` | `now >= cutoff`. Before it, the day is still a plan, and nothing can be "after the tutor was told" yet. |
| `sessions` | `ScheduleDay.View` for each session, so the fields, `state`, `movedTo` and `changes` match the schedule exactly. |
| `changesAfterCutoff` | Every change with `afterCutoff` true on those sessions, ordered by `changedAt`, the attendee's change before the session's when they share a time (as in `View`). `studentName` is null for a session-level change. |

## Web: the tutor page

- `?tutor=T1` (with the usual optional `?date=`) shows the tutor sheet instead of the grid. The same `←`, `Today`, `→` buttons move the date and keep `?tutor=`.
- The Today view's tutor load line (`T1 Ngoc Anh 7 · …`) becomes links to each tutor's sheet for the shown date. The sheet has a `← All rooms` link back to the grid for that date.
- Header: `Ngoc Anh (T1) · Fri 6 Mar`, then `Final since Thu 5 Mar 16:00` or `Not final until Thu 5 Mar 16:00`.
- **Changed after you were told** box, only when `changesAfterCutoff` is not empty. One line per change, built by a pure helper in `labels.ts`, e.g. `16:00 R3 · Do Van Kien cancelled by tutor at 14:40 (tutor sick)` or `14:00 R3 · session moved by family at 10:00 (to 2026-03-07 16:00 in R4)`.
- The sessions as a list by time: `09:00–10:00 R1`, the students with lesson IDs, and the same states as the card (struck through when cancelled, the `moved →` label, `⚑` when flagged, the late-change badge).
- A known tutor with no sessions: `No lessons on this day.` An unknown `?tutor=`: `No tutor T9.` (the API's 404).

## Expected results on this export

The clock is at 2026-03-06 10:00 (+07:00), on a freshly seeded database. Rows 1–10 are read-only. Rows 11–13 run in order after them.

| # | Request | Result |
|---|---|---|
| 1 | `T1`, no date | 200. `date` 2026-03-06, `cutoff` 03-05 16:00, `final` true. 7 sessions (L018, L021, L022, L024–L027), L018 `past`, the rest `upcoming`. `changesAfterCutoff` empty |
| 2 | `T3`, 03-05 | 2 sessions: L014, and L017 cancelled. 1 change after the cut-off: L017 `cancelled` by `tutor` at 14:40, note `tutor sick` |
| 3 | `T2`, 03-03 | 2 sessions: L002, and L005 cancelled. 1 change: L005 `cancelled` by `family` at 08:15 |
| 4 | `T1`, 03-04 | 1 session with 2 attendees (L009 + L010, the exam pair) |
| 5 | `T2`, 03-04 | L008 with `legacyViolation` true, and L012 |
| 6 | `T1`, 03-10 | L033 and L034, both at 09:00 in R1 and R2, L034 flagged |
| 7 | `T3`, 03-07 | L028. `cutoff` 03-06 16:00, `final` **false** |
| 8 | `T2`, 03-09 (Monday) | 200, empty `sessions` and `changesAfterCutoff` |
| 9 | `T9`, 03-06 | 404 |
| 10 | `T1`, `date=2026-13-01` | 400 |
| 11 | Cancel L020's attendee (`family`), then `T3`, 03-06 | L020 cancelled. 2 changes after the cut-off: the attendee `cancelled` (Bui An Nhien), then the session `cancelled` (`studentName` null) |
| 12 | Move L021 (`T1`, 03-06 11:30) → 03-06 14:30, R4 | 409 `tutor-load` (as in phase 18), and `T1`, 03-06 is unchanged |
| 13 | Move L028 (`T3`, 03-07 09:00) → 03-08 10:00, `family`, then `T3`, 03-07 and `T3`, 03-08 | 03-07: L028 cancelled with `movedTo` 03-08 10:00 R3, no change after the cut-off (03-07's cut-off is later today). 03-08: the new session with a `moved` change `from 2026-03-07 09:00 in R3`, `afterCutoff` false |

## Tasks

Tasks in order. The whole phase is one commit at the end.

1. **Sheet builder** (`Domain/TutorDaySheet.cs`, pure)
   - Records: `TutorDaySheetView(TutorId, TutorName, Date, Now, Cutoff, Final, Sessions, ChangesAfterCutoff)` and `TutorChangeView(SessionId, SessionStartsAt, RoomId, Kind, AttendeeId, StudentName, ChangedAt, ChangedBy, Note)`. The name avoids the existing `TutorDay` index record.
   - `BookingPolicy.Cutoff(DateOnly date)`: the cut-off instant for a lesson date. `IsAfterCutoff` uses it, so the rule is written once.
   - `TutorDaySheet.Build(tutor, date, now, sessions, changes, policy, moveTargets)`: keeps the tutor's sessions on that local date, maps each with `ScheduleDay.View`, and flattens the `afterCutoff` changes with the session's time and room and the attendee's name. All times local.

2. **Endpoint** (`Endpoints/TutorEndpoints.cs`, mapped in `Program.cs`)
   - `GET /api/tutors/{id}/day?date=`: load the tutor (404 if missing), then the same reads as `GetSchedule`, filtered by `TutorId`: `StartingOn(day).Where(s => s.TutorId == id).ToDaySessions()`, their changes, and `MoveTargetsAsync`.
   - If the change and move-target reads are now written twice, move them into one helper in `SessionQueries` and use it from both endpoints.
   - OpenAPI metadata: summary, description, `Produces<TutorDaySheetView>`, 400 and 404.

3. **Unit tests** (`TutorDaySheetTests`, no database, the seed plan as in `ScheduleDayTests`)
   - Rows 1–8 of the table above.
   - Edges: a change exactly at the cut-off is listed, one a minute before is not. Another tutor's session on the same day is left out. A session starting at 00:00 local the next day is left out. `final` flips at the cut-off.

4. **Integration tests** (`TutorDayEndpointTests`, `[Collection("api")]`)
   - Read-only: rows 1, 9 and 10 on the pinned day.
   - With writes: on a date no other test uses (check with a grep of the test project first), clock moved as in `MoveSessionTests`. Book a session, move the clock past the cut-off, cancel it: the change is in `changesAfterCutoff`. Move another one to the next day: the old date shows `movedTo`, the new date shows the `from` change.
   - `ApiCalls.TutorDay(client, id, date)` helper.

5. **Web**
   - `api.ts`: `TutorDaySheet` and `TutorChange` types, `fetchTutorDay(id, date?)`. A 404 throws `ApiError` with status 404.
   - `labels.ts`: `changeLine(change)` and `cutoffLine(sheet)`, with cases in `labels.test.ts`.
   - `TutorSheet.tsx`: the header, the changes box and the session list. Reuse `SessionCard`'s pieces (the student line, `movedLabel`, the badge) instead of copying them.
   - `App.tsx`: read `?tutor=` next to `?date=`, and keep both in the URL. The tutor load line becomes links. `layout.ts` is unchanged.
   - `App.css`: the list and the changes box, in light and dark.

6. **Build and test**
   - `dotnet build BrightPath.slnx` (zero warnings, the web build included) and `dotnet test`. `npm test` and `npm run lint` in `web/`.

7. **Run it**
   - Fresh dev DB. Rows 1–13 with curl, in order.
   - The browser with the **Playwright CLI** (checks below).
   - Reseed the dev DB at the end.

8. **Docs in the same commit**
   - `DECISIONS.md` §2 "What I leave broken": tutors can now read their own day, but nothing sends it to them.
   - `DECISIONS.md` §3 API table: the tutor day row. Remove the "Stretch … not built: `GET /api/tutors/{id}/day`" sentence.
   - `DECISIONS.md` §4: phase 19 built, with the real extra time (**ask the author first**). Next week: item 1 becomes sending the sheet (feature 4) and the Q6 "day sent" record, not building the sheet.
   - `specs/roadmap.md` status: phase 19 built.
   - `README.md` Try it: one tutor day example.

9. **Commit**
   - `feat: show one tutor's day with what changed after they were told`. The body says why: the tutor needs one page that is read from the schedule, not a chain of messages, and the changes after the cut-off go at the top because they are what the tutor has not heard yet.

## Verification

The phase can be merged when every item below is true.

### Automated

- [ ] `dotnet build BrightPath.slnx` succeeds with zero warnings (the web project included).
- [ ] `dotnet test` passes: the existing tests, `TutorDaySheetTests` and `TutorDayEndpointTests`.
- [ ] `npm test` and `npm run lint` pass in `web/`.
- [ ] The cut-off edge test fails if `>=` in `IsAfterCutoff` becomes `>` (checked by hand, not committed).

### Endpoint (curl, on a freshly seeded dev DB)

| # | Check | Expected |
|---|---|---|
| 1–13 | Rows 1–13 of "Expected results", in order | As listed there |
| 14 | A session in the sheet vs. the same session in `GET /api/schedule` for that date | The same JSON |
| 15 | Scalar UI / `/openapi/v1.json` | The tutor day endpoint is listed, with 200, 400 and 404 |

### Browser (Playwright CLI, the API and `npm run dev`)

| # | Check | Expected |
|---|---|---|
| 1 | The Today view on 03-06, click `T1 Ngoc Anh 7` | `?tutor=T1&date=2026-03-06`. 7 lessons, L018 shown as past, `Final since Thu 5 Mar 16:00`, no changes box |
| 2 | `?tutor=T3&date=2026-03-05` | L017 struck through. The changes box has `16:00 R3 · Do Van Kien cancelled by tutor at 14:40 (tutor sick)` |
| 3 | `?tutor=T3&date=2026-03-07` | `Not final until Fri 6 Mar 16:00` |
| 4 | `→` and `←` on a sheet | The date moves and `?tutor=` stays |
| 5 | `← All rooms` | The grid for the same date, with no `?tutor=` |
| 6 | After row 13, T3 on 03-07 | L028 struck through with `moved → Sun 8 Mar 10:00 R3`, a link to T3's sheet for 03-08 |
| 7 | `?tutor=T9` | `No tutor T9.` |
| 8 | `?tutor=T2&date=2026-03-09` | `No lessons on this day.` |

### Docs and commit

- [ ] `DECISIONS.md` §2, §3 and §4 are true: the endpoint is in the API table, phase 19 is built with the author's real extra time, and next week no longer lists the sheet.
- [ ] `specs/roadmap.md` status: phase 19 built.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
- [ ] The dev DB is reseeded to the export afterwards.

## Context

- `DECISIONS.md` §1 Q6 (the cut-off), §2 feature 3, §3 (change records, the API table).
- Phase 10 (the schedule read and `ScheduleDay`), phase 13 (cancel), phase 16 (the Today view), phase 18 (move and `movedTo`).
