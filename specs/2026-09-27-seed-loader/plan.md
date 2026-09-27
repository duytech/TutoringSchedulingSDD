# Phase 8: Seed loader — Plan

Tasks in order. The whole phase is one commit at the end.

1. **Bring in the data**
   - Copy `.assignment/lessons_export.csv` and `.assignment/tutors.csv` to `src/BrightPath.Api/Seed/`.
   - Mark them `CopyToOutputDirectory=PreserveNewest` in the `.csproj`, so both the API and the test project find them next to the binaries.
   - Add the `CsvHelper` package.

2. **Policy settings**
   - `BookingPolicyOptions` (`TimeZone`, `CutoffLocalTime`, `LateCancellationWindow`), bound from the `BookingPolicy` section in `appsettings.json`.
   - `BookingPolicy` with two pure functions, reused later by phases 11 and 13:
     - `IsAfterCutoff(changedAt, sessionStartsAt)`: after 16:00 local on the calendar day before the lesson.
     - `IsChargeable(cancelledBy, cancelledAt, sessionStartsAt)`: `family`, and less than the window before the start.
   - `CancelledBy.FromNote(note)`: the first of `family`, `tutor`, `centre` found in the note (case-insensitive), else null.

3. **CSV records**
   - `LessonRow` and `TutorRow` records with CsvHelper maps by header name. A small `SeedCsv.Read…` helper per file.

4. **Planner (pure)**
   - `SeedPlanner.Plan(tutorRows, lessonRows, policy)` returns a `SeedPlan` (tutors, students, sessions with attendees, booking changes). No I/O, no database.
   - Steps: build students by name, then group lesson rows into sessions by `(date, start_time, duration_min, tutor_id, room)`, then map attendees, cancel whole sessions, run the `legacy_violation` pass from `requirements.md`, and add the `cancelled` booking changes.
   - Store every `DateTimeOffset` in UTC.

5. **Writer and startup hook**
   - `SeedLoader.SeedAsync(db, …)`: if `Seed:Enabled` is false or any session exists, return. Otherwise read both CSVs, plan, then `AddRange` and `SaveChangesAsync` inside one transaction.
   - Log one summary line: counts per table and how many rows were flagged `legacy_violation`.
   - Call it in `Program.cs` right after `MigrateAsync`, in the same scope.
   - `Seed:Enabled: true` in `appsettings.json`.

6. **Unit tests** (`SeedPlannerTests`, real CSV, no database)
   - Counts: 3 tutors, 6 students, 33 sessions, 34 attendees, 2 booking changes.
   - L009 and L010 share one session. L033 and L034 do not.
   - `legacy_violation` is set on L034's session and L008's attendee, and on nothing else.
   - L005: `family`, not chargeable, `after_cutoff`. L017: `tutor`, not chargeable, `after_cutoff`. Both sessions cancelled.
   - L015 stays `no_show`, and its session stays active.
   - L018 starts at 2026-03-06 09:00 +07:00 (stored as 02:00 UTC).
   - `BookingPolicy`: one case each side of the 4-hour window and of the 16:00 cut-off, including a Tuesday lesson changed on Monday evening.

7. **Build and test**
   - `dotnet build` (warnings as errors). `dotnet test`.

8. **Run it**
   - Drop the dev DB, `dotnet run`, check that `/health` is `Healthy` and the log shows the summary line.
   - Restart once, and check that nothing is seeded twice.
   - psql checks from `validation.md`.

9. **Docs in the same commit**
   - `DECISIONS.md` §1 "Assumptions I had to invent": the two bullets from `requirements.md`.
   - `specs/tech-stack.md` "Seed history vs. constraints": one sentence saying the flags are computed, and that the constraints check them on insert.

10. **Commit**
    - `feat: seed the exported week as history, flagging rows that break overlap rules`. The body says why: the export is history and is loaded as it is, the flags are computed so the database itself confirms them, and policy functions are shared with the create and cancel phases.
