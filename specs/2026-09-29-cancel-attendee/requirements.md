# Phase 13: Cancel attendee — Requirements

Roadmap: Stage C, phase 13 (est. 12 min). One atomic commit.

## Goal

`POST /api/sessions/{id}/attendees/{attendeeId}/cancel` cancels one student's place in a session. The cancellation:

- frees the slot, so the conflict rules and the constraints stop counting it,
- records who cancelled, and whether it is chargeable (only a **family** cancelling less than 4 hours before, Q2),
- leaves a `booking_changes` row flagged `after_cutoff` when it happens after 16:00 the day before, so "cancelled after the tutor was told" shows up (Q6).

Nothing is deleted. When the last attendee goes, the whole session is cancelled too (`DECISIONS.md` §3).

## In scope

- The endpoint, in the `/api/sessions` group from phase 11.
- A pure cancel check: what the cancel does, or why it is refused. No I/O.
- Unit tests for the check on the real export, plus integration tests on the phase 12 test host.
- Requests in `BrightPath.Api.http`.

## Out of scope

- Cancelling a whole session in one call. Cancel each attendee, and the last one cancels the session.
- Undoing a cancellation. Book again instead.
- Recording a no-show. No endpoint sets `no_show`.
- Billing. `chargeable` is only a flag (`DECISIONS.md` §2, feature 7).
- Move → phase 18.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Already started | A session that has started (`now >= startsAt`, against the clock) cannot be cancelled: **409** `already-started`. `cancelled_at` is always the clock's now and is never backdated. | A lesson that has already started is a no-show or a lesson that happened, not a cancellation. It would also "free" a slot in the past, which means nothing. |
| 200 body | The session view, in the same shape as `GET /api/sessions/{id}`. It holds the attendee's `chargeable`, the new change with `afterCutoff`, and `cancelled` if the session went too. | One shape for a session (as in phase 11). The receptionist sees the result straight away. |
| Change records | One `cancelled` change for the attendee. When that was the last active attendee, a **second** `cancelled` change for the whole session (`attendee_id` null), with the same time and the same `changed_by`. | The tutor's view needs "your 14:00 is gone" as its own line, not something worked out from the attendee rows. |

The seed keeps one change per cancelled row (phase 8). The export does not say when a session was dropped as a whole, and history is not rewritten. Only new cancels write the session change.

## Request

`POST /api/sessions/{id}/attendees/{attendeeId}/cancel`

```json
{ "cancelledBy": "family", "note": "flu" }
```

| Field | Rule |
|---|---|
| `cancelledBy` | Required. `family`, `tutor` or `centre`. |
| `note` | Optional, at most 500 characters. Stored on the attendee's change. |

## Responses

Checked in this order. The first group that fails decides the answer.

| Order | Case | Result |
|---|---|---|
| 1 | Bad body (missing or unknown `cancelledBy`, note too long, malformed JSON) | **400** `ValidationProblem`, naming the field |
| 2 | No such session, or the attendee is not in that session | **404** `ProblemDetails` |
| 3 | Refused | **409** `ProblemDetails` with a `conflicts` list, in the same shape as the create 409. All reasons at once. |
| 4 | Otherwise | **200** with the session view |

### 409 reasons

| Code | When | Message |
|---|---|---|
| `already-started` | `now >= startsAt` | `The session started at 2026-03-06 09:00, before now (2026-03-06 10:00).` (local times) |
| `already-cancelled` | The attendee's status is `cancelled` | `Vu Ha My was already cancelled at 2026-03-03 08:15 by family.` (by … left out when unknown) |

A `no_show` attendee is always on a session that has started, so it gets `already-started`. Only `booked` attendees can be cancelled.

The codes are constants next to `RuleCodes.InThePast`, and are not in `RuleCodes.All`: the violation report never produces them.

## What a cancel writes

In one transaction, with `now` from the clock:

- **Attendee:** `status` = `cancelled`, `cancelled_at` = now, `cancelled_by`, `chargeable` = `BookingPolicy.IsChargeable(cancelledBy, now, startsAt)`.
- **Change:** `kind` = `cancelled`, `attendee_id` = the attendee, `changed_at` = now, `changed_by` = `cancelledBy`, `after_cutoff` = `BookingPolicy.IsAfterCutoff(now, startsAt)`, `note`.
- **When no other attendee is still `booked`:** the session's `cancelled_at` = now, plus a second change with `attendee_id` null and the same `changed_at`, `changed_by` and `after_cutoff`. No note.

The freed slot is free at once: the constraints skip cancelled attendees and cancelled sessions, and `ScheduleRules` does too.

### Two cancels at the same moment

The transaction first locks the session row (`SELECT … FOR UPDATE`) and only then reads its attendees. So two cancels on the same session run one after the other:

- The same attendee twice: the second one sees `cancelled` and gets 409 `already-cancelled`. The change is not written twice.
- The two attendees of a pair at once: the second one sees the first one cancelled, so it cancels the session. Without the lock, both would see the other one still `booked`, and the session would stay active with no one in it.

Read Committed stays the isolation level (as in phase 11).

## Expected results on this export

The clock is at 2026-03-06 10:00 (+07:00). Attendees are named by their lesson ID. The rows run on one freshly seeded database, in order.

| # | Request | Result |
|---|---|---|
| 1 | L020 (03-06 10:30, T3, R3), `family` | 200. `chargeable` true (30 min before). Its change `afterCutoff` true. The session is `cancelled`, with 2 changes |
| 2 | L024 (03-06 16:00), `family` | 200. `chargeable` false (6 h before), `afterCutoff` true: the two rules are independent |
| 3 | L025 (03-06 17:30), `tutor` | 200. `chargeable` false: only a family pays (Q2) |
| 4 | L028 (03-07 09:00), `family` | 200. `chargeable` false, `afterCutoff` false (the cut-off for 03-07 is today 16:00) |
| 5 | L028 again | 409 only `already-cancelled` |
| 6 | Book T3, R3, 03-07 09:00, [Tran Bao Long] (the slot L028 held) | 201: the slot is free |
| 7 | L018 (03-06 09:00, already over) | 409 only `already-started` |
| 8 | L005 (03-03, cancelled) | 409 `already-started` and `already-cancelled` |
| 9 | Book an exam pair T2, R5, 03-07 14:00, [Vu Ha My, Le Minh Chau]. Cancel Vu Ha My (`centre`) | 200. The session stays active, 1 `cancelled` change plus its `created` change |
| 10 | Then cancel Le Minh Chau (`family`) | 200. The session is `cancelled`. Changes: `created`, 2 attendee `cancelled`, 1 session `cancelled` |
| 11 | `cancelledBy` `teacher` / missing / note of 501 chars | 400 |
| 12 | Random session id / L020's attendee under L021's session id | 404 |
| 13 | `GET /api/reports/violations` afterwards | **3** items: the export's 4 without `tutor-load` on 03-06. Rows 2 and 3 took T1 from 7 sessions to 5, and cancelling is how the overload gets fixed |

## Technical notes

- **Cancel check** (`Domain/CancelCheck.cs`): `Decide(session, attendeeId, cancelledBy, now, policy)` over a small read model (start, attendees with status, name, `cancelled_at`, `cancelled_by`). It returns either the conflicts, or the outcome: `chargeable`, `afterCutoff`, `cancelsSession`. The endpoint only loads, locks and writes.
- The session and its attendees are loaded **tracked** (they are updated), after the `FOR UPDATE` lock: `db.Sessions.FromSql($"SELECT * FROM sessions WHERE id = {id} FOR UPDATE")`, then the attendees with their student names.
- The attendee must belong to the session in the path, otherwise 404. This is not a 409, because nothing is wrong with the booking: the URL is wrong.
- `note` goes on the attendee's change, not on the attendee: the attendee's `note` is the CSV note from the export.
- The 200 body reuses `LoadView` from phase 11.
- OpenAPI metadata as in phase 11: `.WithName("CancelAttendee")`, `.Produces<ScheduleSessionView>()`, `.ProducesValidationProblem()`, `.ProducesProblem(404)`, `.ProducesProblem(409)`.

## Docs

- `DECISIONS.md` §3 API table: the cancel row returns the session view. 409 `already-started` / `already-cancelled`. 404.
- `DECISIONS.md` §3 change records: the last attendee's cancel also writes a session `cancelled` change. The seed does not, because history is not rewritten.
- `DECISIONS.md` §3 "Where each rule is enforced": a row for "a started session cannot be cancelled" (code, against the clock), and the row lock that keeps two cancels in order.
- `specs/roadmap.md` phase 13: the session view, `already-started`, and the two change rows.

## Context

- `DECISIONS.md` §1 (Q2, Q6, "cancelled frees the slot"), §3 (data model, change records, API).
- Phase 11 spec (the 409 shape, `LoadView`), phase 12 spec (the test host).
