# Why the write endpoints still open a transaction

The overlap rules are enforced by exclusion constraints, yet `CreateSession`, `Cancel` and `Move` in [`src/BrightPath.Api/Endpoints/SessionEndpoints.cs`](src/BrightPath.Api/Endpoints/SessionEndpoints.cs) each call `BeginTransactionAsync`. This note explains why the two are not in tension.

## What the exclusion constraints were for

They were chosen for **correctness under a race**, not to avoid transactions or to save time. An overlap check done in code can always lose a race; a constraint cannot (see [`DECISIONS.md`](DECISIONS.md), "Where each rule is enforced").

They replace the code check for:

- a room holding one session at a time,
- a tutor being in one room at a time,
- a student being in one place at a time.

Each of these compares **one pair of rows**, which is exactly what `EXCLUDE USING gist (... WITH =, slot WITH &&)` can express. Postgres refuses the second row at insert time, with no lock taken by the app.

## What they cannot do: tutor load

"At most 6 sessions per tutor per day" is a **count across many rows**. An exclusion constraint compares rows two at a time, so it cannot say "no more than N rows". The rule has to be checked in code:

1. read the tutor's sessions for that day,
2. count them (`BookingCheck.Conflicts`),
3. insert the new session.

Without a lock, two receptionists booking the same tutor on the same day can both read 5 sessions, both pass the check, and both insert: 7 sessions. This is a classic check-then-act race.

## Why the lock needs a transaction

`CreateSession` serialises bookings per tutor and day with:

```sql
SELECT pg_advisory_xact_lock(hashtextextended(<tutor:date>, 0))
```

`pg_advisory_xact_lock` is **transaction-scoped**: Postgres releases it only at commit or rollback.

- **Without an explicit transaction**, every statement runs in autocommit. The lock statement is its own transaction, so the lock is released **as soon as that statement finishes**. The read, the check and the insert then run unprotected, and the lock does nothing.
- **With the transaction**, the lock is held from the lock call, through reading `sameDay`, the check and `SaveChangesAsync`, until `CommitAsync`. A second booking for the same tutor and day waits at the lock. Under Read Committed, its read of `sameDay` starts after the first one committed, so it sees the new session.

Side effects of the transaction that come for free:

- An early `409` return, or a caught `ExclusionViolation`, disposes the transaction (`await using`), which rolls it back and releases the lock. Nothing is half-written.
- Inserting the session, its attendees and the `created` change is **not** the reason. A single `SaveChangesAsync` already wraps its own transaction.

## Cancel and move need it for their own reasons

- **Cancel** locks the session row with `SELECT ... FOR UPDATE`. A row lock also lasts only until the transaction ends. It makes two cancels of the same pair take turns, so the second one sees the first and cancels the session as the last one out.
- **Move** calls `SaveChangesAsync` twice: once to give up the old slot (so a move that overlaps its own old slot is not refused by the constraints), then again to insert the new session. Both must succeed or both must roll back. It also takes the old row `FOR UPDATE` and the same tutor-day advisory lock as create.

## What it costs

Very little:

- `SaveChangesAsync` opens a transaction anyway. The explicit one only widens it to include the read, adding a few round-trips.
- The lock is keyed by **(tutor, day)**. Only two requests for the same tutor, same day, at the same moment ever wait for each other. At the scale of one centre that almost never happens.

## Alternative: move the count into the database

The app could stop calling `BeginTransactionAsync` in create by moving the load rule into a `BEFORE INSERT OR UPDATE` trigger on `sessions`. The trigger would take the same advisory lock, count, and `RAISE` above the limit. It runs inside the implicit transaction of `SaveChangesAsync`, so the lock is still held until commit. `DECISIONS.md` already names this as the fix if another writer ever appears.

Trade-offs:

- **The lock does not go away.** It moves from C# to PL/pgSQL.
- The limit is config today (`MaxSessionsPerTutorPerDay`), because the owner may change it. A trigger would need that value in the database.
- A new Postgres error would need mapping to the same `409`, as `ExclusionViolation` is today.
- The code check would stay, so a `409` can still list every conflict at once. The trigger would only be the backstop.
- **Move and cancel would still need their transactions**, for the reasons above.

## Decision

Keep the transaction. It is the cheapest correct way to guard a counting rule, and the exclusion constraints still do what they were chosen for: guard the overlap rules without any lock in the app.
