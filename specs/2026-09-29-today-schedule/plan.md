# Phase 10: Pinned clock + Today read — Plan

Tasks in order. The whole phase is one commit at the end.

1. **Clock**
   - `Domain/FixedTimeProvider.cs`: `sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider`, overriding `GetUtcNow()`.
   - `appsettings.json`: `"Clock": { "Now": "2026-03-06T10:00:00+07:00" }`.
   - `Program.cs`: read `Clock:Now` as `DateTimeOffset?`. Register `new FixedTimeProvider(now)` as the `TimeProvider` singleton when it is set, and `TimeProvider.System` when it is not.

2. **Policy helper**
   - `BookingPolicy.ToLocal(instant)`: the instant as a `DateTimeOffset` in the centre's zone.

3. **Builder** (`Domain/ScheduleDay.cs`)
   - Input read model: the day's sessions (with tutor name, attendees with student name, and changes), all rooms, all tutors, `now`, `BookingPolicy`.
   - Response records: `ScheduleDayView`, `ScheduleSessionView`, `ScheduleAttendeeView`, `ScheduleChangeView`, `RoomDay`, `TutorDay`.
   - Work out `state`, `durationMin` and `changedAfterCutoff`. Convert every time to local. Sort as `requirements.md` says.
   - `SessionState` constants: `past`, `in-progress`, `upcoming`.

4. **Endpoint** (`Endpoints/ScheduleEndpoints.cs`)
   - `MapScheduleEndpoints()` with `MapGroup("/api/schedule")` and `GET /`.
   - `date` is a `DateOnly?`. Default: `policy.LocalDate(time.GetUtcNow())`.
   - Queries: sessions with local start date in `[date 00:00, date+1 00:00)`, with attendees, students and tutor. Then the changes for those IDs. Then rooms and tutors. All `AsNoTracking`.
   - Call `ScheduleDay.Build(...)`, return 200.
   - `.WithName("GetSchedule")`, `.WithSummary`, `.WithDescription`, `.Produces<ScheduleDayView>()`, `.ProducesProblem(400)`.
   - Call it from `Program.cs`.

5. **Unit tests** (`ScheduleDayTests`)
   - On the real export (`SeedPlanner` output mapped to the read model, `now` = 2026-03-06 10:00 +07:00):
     - 03-06: the room and tutor indexes from `requirements.md`, R4–R6 empty, L018 and L019 `past`, the other 8 `upcoming`.
     - 03-03: L005 cancelled, its change `afterCutoff`, `changedAfterCutoff` true.
     - 03-04: the exam pair is one session with 2 attendees. The L008 attendee is `legacyViolation`, and its session is not.
     - 03-10: the L034 session is `legacyViolation`, and its attendee is not. Every session is `upcoming`.
     - 03-05: L015 `no_show` and its session not cancelled.
   - Hand-made cases:
     - `state` edges: `now` == start → `in-progress`. `now` == end → `past`. One tick before start → `upcoming`.
     - Times come out with `+07:00`, not UTC.
     - Ordering: two sessions at the same time sort by room.
     - An empty day still lists every room and tutor.
   - A `FixedTimeProvider` test: `GetUtcNow()` returns the configured instant.

6. **`.http` file**
   - `GET /api/schedule`, `GET /api/schedule?date=2026-03-03`, `GET /api/schedule?date=2026-03-04`.

7. **Build and test**
   - `dotnet build` (warnings as errors). `dotnet test`.

8. **Run it**
   - `dotnet run` on the seeded dev DB. Checks from `validation.md` with `curl`.

9. **Docs in the same commit**
   - `DECISIONS.md` §1: the pinned-today bullet names `Clock:Now`.
   - `DECISIONS.md` §3: the API row for the schedule describes the shape.
   - `specs/tech-stack.md` "Clock" row: `Clock:Now`.

10. **Commit**
    - `feat: pin today's clock and read one day's schedule`. The body says why: every time-based rule (today, the 4-hour window, the cut-off) must read one injected clock so the demo and the tests are repeatable, and one day's schedule as JSON is the smallest read that shows the rules hold, cancellations and changes included.
