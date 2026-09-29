# Phase 18: Move session — Requirements

Roadmap: below the ✂️ cut line, stretch phase 18 (est. 12 min). One atomic commit.

## Goal

`POST /api/sessions/{id}/move` moves a lesson to a new time, room or length **without overwriting anything**. In one transaction it cancels the old session, creates the new one, and points `moved_to_session_id` from the old to the new (`DECISIONS.md` §3). The tutor can see both "your 14:00 is gone" and "it is now at 16:00". Today, a move is a cancel plus a new booking, and nothing links the two.

The new slot is checked by the same rules as a new booking, with the old session left out, since it is being given up.

## In scope

- The endpoint, in the `/api/sessions` group.
- A pure move check, built from `CancelCheck` (for the old session) and `BookingCheck` (for the new slot).
- `movedTo` on the session view, so a client can show where a moved session went.
- A "moved →" label on the old session's card in the Today view.
- Unit tests, integration tests on the phase 12 host (one race test), and a check in the browser with the Playwright CLI.
- Docs in the same commit.

## Out of scope

- Changing the tutor. A different tutor is a different lesson: cancel it and book a new one.
- Moving one student of a pair on their own. The whole session moves.
- Charging for a move (Q8 below).
- The tutor day endpoint (phase 19).

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| What can change | `startsAt` (required), `roomId` and `durationMin` (optional, they default to the old values). The tutor stays. If nothing changes, the answer is 400. | The tutor is who the lesson is with. Changing them is a new booking, and both tutors would need to know. |
| Chargeable | A move is **never chargeable**, even a family move inside 4 hours: the lesson still happens. New owner question **Q8** in `DECISIONS.md` §1, saying where the code would change if the owner says otherwise. | Q2 covers cancellations. A move is not one. |
| Change records | A `moved` change on **both** sessions (`attendee_id` null, `changed_by` = `movedBy`). The old one's `after_cutoff` is against the old start, with the note `to 2026-03-07 16:00 in R4`. The new one's is against the new start, with the note `from 2026-03-07 14:00 in R3`. | The tutor sees the loss and the new slot, each flagged if it came after the tutor was told. |
| UI | The old session's card shows `moved → 16:00 R4` (same day) or `moved → Wed 11 Mar 16:00 R4` (another day, a link to that date). | The whole point of a linked move is that the tutor can see where the lesson went. |

## Request

`POST /api/sessions/{id}/move`

```json
{ "startsAt": "2026-03-07T16:00:00+07:00", "roomId": "R4", "durationMin": 60, "movedBy": "family", "note": "exam moved" }
```

| Field | Rule |
|---|---|
| `startsAt` | Required. ISO-8601 with an offset, as in create. |
| `roomId` | Optional. A known room. Defaults to the old room. |
| `durationMin` | Optional. 60 or 90. Defaults to the old length. |
| `movedBy` | Required. `family`, `tutor` or `centre`. |
| `note` | Optional, at most 500 characters. Added to both `moved` changes after the from/to text. |

At least one of time, room or length must differ from the old session. Otherwise it is a 400 on `startsAt`: `Nothing to move: same time, room and length.`

## Responses

Checked in this order:

| Order | Case | Result |
|---|---|---|
| 1 | Bad body | **400** `ValidationProblem`, naming the field |
| 2 | No such session | **404** |
| 3 | Refused | **409** `ProblemDetails` with `conflicts`, all at once (below) |
| 4 | Otherwise | **201**, `Location: /api/sessions/{newId}`, and the new session view |

### 409 reasons

About the **old** session, from `CancelCheck` rules:

| Code | When |
|---|---|
| `already-started` | The old session has started (`now >= startsAt`). A lesson that has begun is not moved. |
| `already-cancelled` | The old session is cancelled, which includes one already moved. Message: `The session was already cancelled at 2026-03-07 10:00.` |

About the **new** slot, from `BookingCheck`, with the old session left out of the day:

`in-the-past`, `room-overlap`, `tutor-overlap`, `student-overlap`, `tutor-load`, `closed-day`, `outside-hours`, `too-many-attendees`. These are the same codes and messages as a new booking.

A move inside a day that is already over the tutor load is refused with `tutor-load`. For example, moving one of T1's 7 sessions on 03-06 to another time on the same day still leaves 7. Cancel one first. This follows from Q5 (no override) and is said in §4.

A race past the code checks (a constraint error `23P01` on the insert) becomes the same 409 as in create.

## What a move writes

In one transaction, with `now` from the clock:

1. Lock the old session row (`FOR UPDATE`), as cancel does, so two moves or a move and a cancel of the same session take turns.
2. Take the tutor-day advisory lock for the **new** date (`BookingLocks.TutorDay`), as create does.
3. Read the new day's active sessions, leave out the old one, and run the checks.
4. **Cancel the old session:** `cancelled_at` = now. Every `booked` attendee becomes `cancelled`, with `cancelled_at` = now, `cancelled_by` = `movedBy` and `chargeable` = false. An attendee who was already cancelled stays as it is. Save this **before** the insert, so a move that overlaps the old slot (14:00 → 14:30 in the same room) does not hit the constraints on the old row.
5. **Create the new session:** the same tutor, the new time, room and length, and one `booked` attendee for each student who was booked on the old one.
6. Set the old session's `moved_to_session_id` to the new id. Add the two `moved` changes.
7. Commit.

Cancelled attendees on the old session get no attendee change of their own. The session's `moved` change says what happened to all of them.

## `movedTo` on the session view

The schedule view (and `GET /api/sessions/{id}`) gains `movedTo: { id, startsAt, roomId } | null`. It is null unless `movedToSessionId` is set. The target may be on another day, so it is loaded by id with one small extra query, not taken from the day's own sessions. `movedToSessionId` stays as it is.

## Web: the "moved →" label

On a cancelled card with `movedTo`:

- Same date: `moved → 16:00 R4`.
- Another date: `moved → Wed 11 Mar 16:00 R4`. It is a link that shows that date (`?date=`).

The label is not struck through, even though the card is.

## Expected results on this export

The clock is at 2026-03-06 10:00 (+07:00). The rows run in order on one freshly seeded database.

| # | Request | Result |
|---|---|---|
| 1 | L028 (03-07 09:00, 90 min, T3, R3) → 03-07 14:00, `family` | 201. The old session is cancelled with `movedTo` set to the new one. Tran Bao Long is `booked` on the new one and `cancelled` (not chargeable) on the old. Both `moved` changes have `afterCutoff` false (the cut-off for 03-07 is today 16:00) |
| 2 | L020 (03-06 10:30, T3, R3) → 03-06 16:00, `family` | 201. Both changes `afterCutoff` true. `chargeable` false, although it is 30 min before |
| 3 | L029 (03-07 11:00, T2, R2) → 03-07 11:30, same room | 201. It overlaps its own old slot, and that is fine: the old slot is freed first |
| 4 | L030 (03-07 15:00, T1, R1) → 03-07 14:00, R3 | 409 only `room-overlap`, against the session from row 1 |
| 5 | L034 (03-10 09:00, T1, R2, flagged) → 03-10 10:00 | 201. The new session is not `legacyViolation`. The violation report loses the 03-10 `tutor-overlap`, so it has **3** items |
| 6 | L021 (03-06 11:30, T1) → 03-06 14:30, R4 | 409 only `tutor-load`: T1 still has 7 that day |
| 7 | L030 → 03-09 10:00 (Monday) | 409 only `closed-day` |
| 8 | L030 → 03-05 10:00 | 409 only `in-the-past` |
| 9 | L018 (03-06 09:00, T1, R1) → 03-07 10:00, R4 | 409 only `already-started` (the new slot itself is free) |
| 10 | The old L028 session from row 1, moved again → 03-07 17:00 | 409 only `already-cancelled` |
| 11 | L005 (03-03, cancelled) → 03-07 16:00 | 409 `already-started` and `already-cancelled` |
| 12 | L030 with the same time, room and length / `movedBy` `teacher` / no offset on `startsAt` / `durationMin` 45 | 400, naming the field |
| 13 | A random session id | 404 |

## Technical notes

- **Move check** (`Domain/MoveCheck.cs`, pure): `Conflicts(old, candidate, sameDayWithoutOld, now, policy)` returns the old-session conflicts (reusing the `CancelCheck` start and cancelled rules, but with a session-level `already-cancelled` message) followed by `BookingCheck.Conflicts(candidate, …)`. It also builds the two change notes.
- **Endpoint:** reuse the create validation helpers (the offset parse, the durations) and the cancel row lock (`FromSql … FOR UPDATE`). Two `SaveChanges` calls inside one transaction: the cancel first, then the insert with the link and the changes.
- **View:** `ScheduleDay.View` gets the `movedTo` target from the caller. `ScheduleEndpoints` and `SessionEndpoints.LoadView` load the targets by id.
- **Web:** `api.ts` gains `movedTo`. `SessionCard` shows the label. The date link reuses the page's `?date=` handling.

## Docs

- `DECISIONS.md` §1: Q8 (is a late family move chargeable?), with what changes if yes: `MoveCheck` would use `BookingPolicy.IsChargeable` on the old attendees.
- `DECISIONS.md` §3: the move bullet is no longer "stretch". The API table gets the move row. A rule row says a move cannot start a lesson that has begun, and that the new slot passes the create rules.
- `DECISIONS.md` §4: Where it stopped (phase 18 built, with the real extra time from the author). Next week loses "Move". Weak spots: the flagged-L034 bullet says moving it is now one call, and a move inside an over-loaded day is refused.
- `specs/roadmap.md` status: phase 18 built.
- `README.md` Try it: one move example (optional, if it stays short).

## Context

- `DECISIONS.md` §3 (change records, the move bullet, the rejected `PUT`).
- Phase 11 (create checks, the tutor-day lock), phase 13 (cancel checks, the row lock), phase 16 (the card).
