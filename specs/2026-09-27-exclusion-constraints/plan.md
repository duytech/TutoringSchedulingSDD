# Phase 7: Schema, exclusion constraints — Plan

Tasks in order. The whole phase is one commit at the end.

1. **Declare the extension in the model**
   - `modelBuilder.HasPostgresExtension("btree_gist")` in `BrightPathDbContext.OnModelCreating`.

2. **Generate the migration**
   - `dotnet ef migrations add ExclusionConstraints -p src/BrightPath.Api -o Data/Migrations`.
   - It should hold only the extension change (`AlterDatabase` with the `btree_gist` annotation).

3. **Add raw SQL to `Up`**, in this order:
   1. `ALTER TABLE sessions ADD COLUMN slot tstzrange GENERATED ALWAYS AS (tstzrange(starts_at, ends_at, '[)')) STORED NOT NULL`.
   2. `ALTER TABLE attendees ADD COLUMN slot tstzrange NOT NULL` (the tables are empty until phase 8, so no backfill).
   3. The `attendees_copy_slot()` function and the `trg_attendees_copy_slot` trigger from `requirements.md`.
   4. The three `EXCLUDE` constraints, with the names from `requirements.md`. Build the `'cancelled'` literal from `AttendeeStatus.Cancelled`, the same way the CHECKs do.

4. **Write `Down`** in reverse: drop the constraints, the trigger, the function, then both `slot` columns. The EF-generated part drops the extension.

5. **Build and test**
   - `dotnet build` (warnings as errors).
   - `dotnet test`: `SchemaTests` must still report no pending model changes, which proves the unmapped columns do not confuse EF.

6. **Run it**
   - Drop the dev DB, `dotnet run`, check that the migration applies and `/health` is `Healthy`.
   - Check that `Down` works: `dotnet ef database update InitialSchema`, then `dotnet ef database update` again.
   - Manual psql checks from `validation.md`.

7. **Docs in the same commit**
   - `specs/tech-stack.md` "Where each rule is enforced": show the real predicates (`cancelled_at IS NULL AND NOT legacy_violation`, `status <> 'cancelled' AND NOT legacy_violation`) instead of `WHERE (active)`, and say that `attendees.slot` is filled by a trigger.
   - `DECISIONS.md` §3: one bullet on the trigger and one on the invariant that cancelling a session cancels its attendees.

8. **Commit**
   - `feat: enforce room, tutor and student overlaps with exclusion constraints`. The body says why: overlap checks in code can lose a race, and the trigger means no insert path can forget the attendee slot.
