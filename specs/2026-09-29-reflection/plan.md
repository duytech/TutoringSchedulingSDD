# Phase 15: DECISIONS §4: reflection — Plan

Tasks in order. The whole phase is one commit at the end.

1. **Get the author's input**
   - Ask for the real total time and what took longer than estimated. Do not work it out from commit times.
   - Show the draft "Where the AI helped" and "An AI suggestion I threw away" text, and keep only what the author confirms as true.

2. **Check each weak spot against the code**
   - Flagged rows outside the constraints: the `WHERE … NOT legacy_violation` predicates in the migration, and `SeedPlanner` flagging L034's session and L008's attendee.
   - Load and pair limit in code only: `BookingCheck` and `ScheduleRules`, and no trigger in the migrations.
   - Seed only into an empty database: `SeedLoader`.
   - Tests isolated by date: `SessionConflictTests`, `BookingRaceTests`, `CancelAttendeeTests`.
   - Tie ordering: `ScheduleDay.View`.
   - Drop any candidate that turns out not to be true, and add any weak spot found on the way.

3. **Write `## 4. Reflection`** at the end of `DECISIONS.md`
   - Subsections in the order of `requirements.md`: Where it stopped, Next week, Known weak spots, Where the AI helped, An AI suggestion I threw away.
   - A few bullets each. Use lesson IDs and file names as evidence.

4. **Small consistency edits**
   - `DECISIONS.md` §2 "What I leave broken": the board line says the stretch phase was not built.
   - `specs/roadmap.md`: under the cut line, one line saying phases 16–19 were not built and §4 says so.

5. **Read it as the interviewer**
   - Every claim can be pointed to in the code, the data or the history.
   - Nothing is repeated from §1–§3 without adding something.
   - Links and section references resolve.

6. **Commit**
   - `docs: reflect on what is next, what is weak and how the AI was used`. The body says why: the brief asks for an honest close, the stretch phases were cut to stay near the time box, and the AI parts are written as they happened.
