# Exclusion Constraints: Maintenance as the Project Grows

What gets hard about the three `EXCLUDE` constraints (phase 7) once there are more features and more conflict rules, and how to keep that manageable.

## Rule of thumb

The database holds only **physical, stable** rules: "one resource cannot be held by two things at the same time". Anything that is policy, may change, or needs counting stays in code. Kept this way, the number of constraints grows very slowly even when features grow fast.

## Which rules fit `EXCLUDE`

`EXCLUDE` can only say one thing: **two rows conflict** when they share a key and their time ranges overlap.

| Kind of rule | Fits `EXCLUDE`? | Where it goes |
|---|---|---|
| Room, tutor, student or equipment double-booked | Yes | Database |
| At most N (6 a day, room capacity, 2 per session) | No. It counts rows, it does not compare a pair. | Code, plus a lock (advisory lock or `SELECT … FOR UPDATE`) |
| Opening hours, closed days, cut-off, notice period | No. It is policy the owner may change. | Code and config |
| A 15-minute gap between a tutor's sessions | Possible, by widening `slot` | Either. See "Many resource types" below. |

Most new conflict rules land in the second and third rows, so they do not add constraints.

## What is hard

### Changing a predicate means dropping and re-creating the constraint

- PostgreSQL has no `ALTER` for an exclusion constraint. It also does not support `NOT VALID` or `CONCURRENTLY` for one.
- Re-creating it rebuilds the GiST index and scans the whole table under an `ACCESS EXCLUSIVE` lock. For a tutoring centre that takes seconds. For a table with tens of millions of rows it needs a maintenance window.
- Existing rows may break the new rule. They must be fixed or flagged first, the way `legacy_violation` handles the seeded overlaps.

### The same predicate lives in two places

The code checks every rule first, so a 409 can list all conflicts in plain words. The database is the backstop. If the two disagree on what "holds the slot" means, the API gives confusing answers.

- Build both from shared constants, as the migration does with `AttendeeStatus.Cancelled`.
- Test both layers against a real database (phase 12). The 11 cases in `2026-09-27-exclusion-constraints/validation.md` should become automated tests.

### `attendees.slot` is a copy

- It is safe today because a session's time never changes after it is created.
- If a later feature lets a session change time in place, add an `AFTER UPDATE` trigger on `sessions` that updates its attendees' `slot`. The alternative is to keep "a move creates a new session" and record that as a decision.

### Invariants spread across code paths

- "Cancelling a session cancels its attendees" is kept by code today.
- Once several paths can cancel (API, background job, admin tool), one of them will forget. At that point, move the invariant into a trigger so the database keeps it.

### Mapping constraint names to conflict kinds

- Phase 11 maps each constraint name to a conflict kind (`room-overlap`, `tutor-overlap`, `student-overlap`).
- Add a test that lists `pg_constraint WHERE contype = 'x'` and fails if any name is missing from the mapping. A new constraint then cannot silently turn into a 500.

## Many resource types: one claims table

If more resources appear (equipment, online rooms, transport), a `slot` column, trigger and constraint per resource does not scale. The usual pattern moves "who holds what, when" into one table:

```sql
CREATE TABLE resource_claims (
    session_id    uuid NOT NULL REFERENCES sessions,
    resource_type text NOT NULL,   -- 'room' | 'tutor' | 'student' | 'projector' ...
    resource_id   text NOT NULL,
    slot          tstzrange NOT NULL,
    active        boolean NOT NULL,
    EXCLUDE USING gist (resource_type WITH =, resource_id WITH =, slot WITH &&)
        WHERE (active)
);
```

- **One constraint.** A new resource type is new rows, not a migration.
- A per-resource buffer (tutor needs 15 minutes, room needs 10 to clean) goes into the claim's `slot`, and code decides the value.
- Cancel and move turn off the session's claims with one `UPDATE`, whatever the resource types.
- **Trade-off:** more duplicated data, and the conflict kind comes from the clashing row's `resource_type` instead of the constraint name.

## Decision for now

Keep the three named constraints. They are easier to read and to explain than a claims table. Move to `resource_claims` when a fourth resource type appears, or when resources need different buffers.
