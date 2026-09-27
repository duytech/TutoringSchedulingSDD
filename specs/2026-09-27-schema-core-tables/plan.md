# Phase 6: Schema, core tables — Plan

Tasks in order. Each is small. The whole phase is one commit at the end.

1. **Tooling**
   - `dotnet new tool-manifest` at the repo root, then `dotnet tool install dotnet-ef` (version 10.x).
   - Add `Microsoft.EntityFrameworkCore.Design` (PrivateAssets=all) and `EFCore.NamingConventions` to `BrightPath.Api.csproj`.

2. **Entities** in `src/BrightPath.Api/Data/` (or `Domain/`, matching the existing `Data` folder)
   - `Tutor`, `Room`, `Student`, `Session`, `Attendee`, `BookingChange` as plain classes with the columns in `requirements.md`.
   - Status, cancelled-by and change-kind values as string constants in one static class per concept (`AttendeeStatus`, `CancelledBy`, `ChangeKind`), so the CHECKs and the code share the same literals.
   - Navigation properties only where later phases need them: `Session.Attendees`, `Session.Tutor`, `Session.Room`, `Attendee.Student`, `Attendee.Session`.

3. **Configuration**
   - One `IEntityTypeConfiguration<T>` per entity. `ApplyConfigurationsFromAssembly` in `OnModelCreating`.
   - Keys, required columns, FKs with `DeleteBehavior.Restrict`, unique indexes (`students.name`, `attendees.source_lesson_id`, `attendees(session_id, student_id)`), index `booking_changes(session_id, changed_at)`.
   - CHECK constraints through `ToTable(t => t.HasCheckConstraint(...))`, with the names from `requirements.md`.
   - `Room` `HasData` for `R1`–`R6`.
   - Guid keys: `ValueGeneratedNever()` is **not** set; let Npgsql generate v7 client-side, and allow code to set the key explicitly.

4. **DbContext wiring**
   - `UseNpgsql(...).UseSnakeCaseNamingConvention()` in `Program.cs`.

5. **Migration**
   - `dotnet ef migrations add InitialSchema -p src/BrightPath.Api`.
   - Read the generated migration: table and column names are snake_case, CHECKs and indexes are present, rooms are inserted, and there is no `slot` column.

6. **Test**
   - `tests/BrightPath.Api.Tests/SchemaTests.cs`: build the `DbContext` with Npgsql options (no connection opened) and assert `context.Database.HasPendingModelChanges()` is false.

7. **Run it**
   - `dotnet build` (warnings as errors).
   - `dotnet run --project src/BrightPath.Api` against the dev DB: migration applies on startup, `/health` is `Healthy`.
   - Manual psql checks from `validation.md`.

8. **Docs in the same commit**
   - `DECISIONS.md` §3 data model: uuid keys, `source_lesson_id`, `note` on attendees.
   - `specs/roadmap.md` phase 7: `tsrange` → `tstzrange`.

9. **Commit**
   - `feat: add core schema (tutors, rooms, students, sessions, attendees, booking_changes)` with a body that says why: CHECKs in the DB for values that must never be wrong, slot/EXCLUDE left to phase 7, uuid keys so sessions and attendees can be built in one unit of work.
