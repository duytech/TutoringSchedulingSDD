# Phase 13: Cancel attendee — Plan

Tasks in order. The whole phase is one commit at the end.

1. **Codes**
   - `RuleCodes.AlreadyStarted = "already-started"`, `RuleCodes.AlreadyCancelled = "already-cancelled"`. Not in `All`, next to `InThePast`.

2. **Cancel check** (`Domain/CancelCheck.cs`)
   - Read model: `CancelSession(Guid Id, DateTimeOffset StartsAt, IReadOnlyList<CancelAttendee> Attendees)`, `CancelAttendee(Guid Id, string StudentName, string Status, DateTimeOffset? CancelledAt, string? CancelledBy, string? LessonId)`.
   - `CancelCheck.Decide(session, attendeeId, cancelledBy, now, policy)` returns `CancelDecision(IReadOnlyList<ScheduleViolation> Conflicts, bool Chargeable, bool AfterCutoff, bool CancelsSession)`.
     - `already-started` if `now >= StartsAt`, then `already-cancelled` if the attendee is `cancelled`. Messages in local time, culture-invariant, as in `requirements.md`.
     - Otherwise `Chargeable` and `AfterCutoff` from `BookingPolicy`, and `CancelsSession` when no other attendee is `booked`.
   - Each conflict's `date` is the session's local date, `sessionIds` the session, `lessonIds` the attendee's lesson ID if it has one.

3. **Endpoint** (`Endpoints/SessionEndpoints.cs`)
   - `CancelAttendeeRequest(string? CancelledBy, string? Note)`.
   - `POST /{id:guid}/attendees/{attendeeId:guid}/cancel`:
     1. Validate the body (400).
     2. Begin a transaction. `FromSql … FOR UPDATE` on the session. Missing → 404.
     3. Load its attendees (tracked) with student names. Attendee not among them → 404.
     4. `CancelCheck.Decide`. Conflicts → 409 through the phase 11 `Conflict` helper.
     5. Update the attendee, add its change, and, if `CancelsSession`, set the session's `cancelled_at` and add the session change. `SaveChanges`, commit.
     6. 200 with `LoadView`.
   - OpenAPI metadata (`requirements.md`).

4. **Unit tests** (`CancelCheckTests`)
   - On the real export (`SeedPlanner`, clock at 2026-03-06 10:00): rows 1, 2, 3, 4, 7 and 8 of the table in `requirements.md`.
   - Hand-made cases:
     - `now` == start → `already-started`. One minute before → allowed.
     - Family exactly 4 h before → not chargeable. One minute less → chargeable.
     - A pair: cancelling one leaves `CancelsSession` false. With the other one already `cancelled` → true.
     - `already-cancelled` message with and without `cancelled_by`.

5. **Integration tests** (`CancelAttendeeTests`, `[Collection("api")]`)
   - The shared host's clock is 03-06, and phase 12 keeps 03-06 and 03-07 as the export. So these tests use `factory.WithWebHostBuilder(b => b.UseSetting("Clock:Now", "2026-03-24T10:00:00+07:00"))` (a Tuesday) and book their own sessions on 03-24 and 03-25, in rooms and times no other test uses.
   - Tests:
     - Family cancel 30 min before (03-24 10:30) → 200, `chargeable`, change `afterCutoff`, session `cancelled`, 2 `cancelled` changes. Then booking the same slot again → 201.
     - Pair on 03-25: cancel one → session active. Cancel the other → session `cancelled`, the session change is there.
     - The same attendee twice → 409 only `already-cancelled`.
     - L018 (03-06 09:00) on the shared host, where the clock is 03-06 10:00 → 409 only `already-started`. A refused cancel writes nothing, so 03-06 stays as the export.
     - 404 for an attendee under the wrong session. 400 for `cancelledBy` `teacher`.
   - **Race** (as in phase 12): a pair on 03-25. The test opens its own transaction, takes `SELECT … FOR UPDATE` on the session, and cancels attendee A by hand (status, `cancelled_at`, change), without committing. Start the API cancel of B. `ApiCalls.WaitUntilBlocked` on `wait_event_type = 'Lock'`. Commit. Expect 200 with the session `cancelled`. Without the row lock, the API sees A still `booked` and leaves the session active, so the test fails.

6. **`.http` file**
   - Cancel by family, cancel by tutor, cancel again (409), bad `cancelledBy` (400). Ids pasted from `/api/schedule`.

7. **Build and test**
   - `dotnet build` (warnings as errors). `dotnet test`.

8. **Run it**
   - Reseed the dev DB (drop `brightpath` with `psql`, then `dotnet run`). Checks from `validation.md` with `curl`. Reseed again at the end.

9. **Docs in the same commit**
   - `DECISIONS.md` §3 and `specs/roadmap.md` phase 13 (`requirements.md` "Docs").

10. **Commit**
    - `feat: cancel an attendee, freeing the slot and flagging late changes`. The body says why: a cancellation frees the slot the rules count, records who cancelled so only a late family cancel is chargeable, and leaves a change row after the cut-off so the tutor can see what changed after they were told. The row lock keeps two cancels on one session in order, so the last one out always cancels the session.
