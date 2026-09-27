# Phase 9: Seed violation report — Plan

Tasks in order. The whole phase is one commit at the end.

1. **Policy settings**
   - Add `MaxSessionsPerTutorPerDay`, `MaxAttendeesPerSession`, `ClosedDays` (`DayOfWeek[]`), `OpensAt`, `ClosesAt` to `BookingPolicyOptions` and to the `BookingPolicy` section in `appsettings.json`.
   - Update `TestPolicy.Create()` in the tests.

2. **Rule set** (`Domain/ScheduleRules.cs`)
   - Read model: `RuleSession` (id, tutor id and name, room id, starts at, ends at, cancelled) and `RuleAttendee` (student id and name, status, lesson ID).
   - `ScheduleViolation(Rule, Date, SessionIds, LessonIds, Message)`.
   - `RuleCodes`: constants for the seven codes.
   - `ScheduleRules.Check(sessions, policy)`: drop cancelled sessions and cancelled attendees, run each rule from `requirements.md`, then sort.
   - Local times come from `BookingPolicy` (add a `LocalTime(instant)` helper next to `LocalDate`).

3. **Endpoint** (`Endpoints/ReportEndpoints.cs`)
   - `MapReportEndpoints()` with `MapGroup("/api/reports")` and `GET /violations`.
   - Parse `from` / `to` as `DateOnly?`. If `from > to`, return 400 `ProblemDetails`.
   - One `AsNoTracking` query for the sessions in range, with attendees, students and tutor. Map them to the read model, run `ScheduleRules.Check`, and return `{ violations }`.
   - `.WithName`, `.WithSummary`, `.Produces<…>(200)`, `.ProducesProblem(400)`.
   - Call it from `Program.cs`.

4. **Unit tests** (`ScheduleRulesTests`)
   - On the real export (`SeedPlanner` output mapped to the read model): exactly the 4 items from `requirements.md`, in that order, with those lesson IDs.
   - Hand-made cases, one per rule and edge:
     - `room-overlap` refused. Touching sessions (10:00 end, 10:00 start) are not an overlap.
     - `outside-hours`: starts 08:30 → broken. Ends 21:30 → fine.
     - `too-many-attendees`: 3 booked → broken. 2 booked + 1 cancelled → fine.
     - `tutor-load`: 7 sessions → broken. 6 sessions + 1 cancelled → fine. An exam pair counts as 1.
     - `student-overlap` with a `no_show` → broken. With a `cancelled` attendee → fine.
     - A cancelled session breaks nothing.

5. **`.http` file**
   - Fix `@host` to the port in `launchSettings.json` if it differs.
   - Add `GET /api/reports/violations` and a filtered request.

6. **Build and test**
   - `dotnet build` (warnings as errors). `dotnet test`.

7. **Run it**
   - `dotnet run` on the seeded dev DB. Checks from `validation.md` with `curl`.

8. **Docs in the same commit**
   - `DECISIONS.md` §3: the API row for the report, and the "one rule set" bullet.
   - `specs/tech-stack.md` "Repository layout": seed CSVs in `src/BrightPath.Api/Seed/`.
   - `specs/roadmap.md` phase 11: reuses `ScheduleRules` and the phase 9 policy settings.

9. **Commit**
   - `feat: report every rule the loaded schedule breaks`. The body says why: the export is loaded as history, so its breaks must be visible somewhere, and the rules are written once so the report and the 409 in phase 11 always agree.
