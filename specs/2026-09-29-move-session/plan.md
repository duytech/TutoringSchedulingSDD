# Phase 18: Move session — Plan

Tasks in order. The whole phase is one commit at the end.

1. **Move check** (`Domain/MoveCheck.cs`)
   - Pure. The inputs are the old session (start, cancelled, room, length, and attendees with status), the candidate `RuleSession`, the new day without the old session, `now`, and the policy.
   - Old-session conflicts: `already-started`, then `already-cancelled` (session level). Then `BookingCheck.Conflicts`.
   - `MoveNotes(old, new, policy)`: `to 2026-03-07 16:00 in R4` and `from 2026-03-07 14:00 in R3`, local and culture-invariant.

2. **View: `movedTo`**
   - `ScheduleSessionView` gains `MovedTo` (`MovedToView(Guid Id, DateTimeOffset StartsAt, string RoomId)`, local times).
   - `ScheduleDay.View` / `Build` take a lookup of move targets. The callers (`ScheduleEndpoints`, `SessionEndpoints.LoadView`) load the targets for the sessions with a `MovedToSessionId`.
   - Existing `ScheduleDayTests` pass an empty lookup.

3. **Endpoint** (`SessionEndpoints`)
   - `MoveSessionRequest(string? StartsAt, string? RoomId, int? DurationMin, string? MovedBy, string? Note)`.
   - `POST /{id:guid}/move`:
     1. Validate the body (reusing the create offset parse and durations, and the cancel `cancelledBy` list).
     2. Transaction. `FOR UPDATE` on the old session (404 if missing). Load its attendees with names.
     3. "Nothing to move" → 400.
     4. The tutor-day advisory lock for the new date.
     5. Load the new day's active sessions, leave out the old id, and run `MoveCheck`. Conflicts → 409.
     6. Cancel the old session and its booked attendees. `SaveChanges`.
     7. Insert the new session and attendees, set `MovedToSessionId`, add the two `moved` changes. `SaveChanges`. `23P01` → the race 409, as in create.
     8. Commit. 201 with `Location` and the view.
   - OpenAPI metadata.

4. **Unit tests** (`MoveCheckTests`)
   - On the export (clock 03-06 10:00): rows 1–11 of `requirements.md` as check results (no database).
   - Edges: the old start equal to now is `already-started`. A move overlapping its own old slot has no conflicts. The notes' text.

5. **Integration tests** (`MoveSessionTests`, `[Collection("api")]`, clock moved to 2026-03-24 10:00, dates 03-26 and 03-27)
   - Move a pair where one student was cancelled earlier: 201, only the booked student moves, the old session is cancelled with `movedTo`, both `moved` changes are there.
   - A move overlapping its own old slot in the same room: 201 (the first `SaveChanges` really runs before the insert).
   - A move into a room that is taken: 409 `room-overlap`, and nothing changed (the old session is still active).
   - A move of an already-moved session: 409 `already-cancelled`.
   - **Race:** the test holds `FOR UPDATE` on a session and cancels it by hand without committing. The API move waits (`ApiCalls.WaitUntilBlocked`, `Lock`). Commit. Expect 409 `already-cancelled`. Without the row lock, the API would move a cancelled session.
   - `GET /api/schedule` for the old date: `movedTo` is set to the new id, time and room.

6. **Web**
   - `api.ts`: `movedTo`.
   - `SessionCard`: the label (same day, or another day as a link). `App` passes a `go(date)` for the link.
   - `layout.ts` is unchanged. Vitest for any new pure helper (the label text).

7. **Build and test**
   - `dotnet build BrightPath.slnx` (zero warnings, which includes the web build) and `dotnet test`. `npm test` and `npm run lint` in `web/`.

8. **Run it**
   - Fresh dev DB. The rows of `requirements.md` with curl, in order.
   - The browser, with the **Playwright CLI**: 03-07 after row 1 shows L028 struck through with `moved → 14:00 R3`. After row 5, 03-10 shows `moved → 10:00 R2` on L034. A move to another day shows the date link, and clicking it opens that date.
   - Reseed the dev DB at the end.

9. **Docs in the same commit** (`requirements.md` "Docs")
   - Ask the author for the real extra time before editing §4.

10. **Commit**
    - `feat: move a session, linking the old slot to the new one`. The body says why: a move after the tutor was told must not overwrite what they were told, so the old session is cancelled and linked to the new one in one transaction, with a `moved` change on each side. The new slot passes the same rules as a new booking, and the row lock keeps a move and a cancel of the same session in order.
