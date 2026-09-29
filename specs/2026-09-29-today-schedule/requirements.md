# Phase 10: Pinned clock + Today read — Requirements

Roadmap: Stage C, phase 10 (est. 8 min). One atomic commit.

## Goal

Two things that the rest of Stage C builds on:

1. **A pinned clock.** "Now" is Friday 2026-03-06 10:00 (+07:00) (`DECISIONS.md` §1, "Assumptions I had to invent"). Everything that needs the time (today's date here, the 4-hour window and the cut-off in phases 11 and 13) reads it from an injected `TimeProvider`. Nothing calls `DateTime.Now` or `DateTimeOffset.UtcNow`.
2. **`GET /api/schedule?date=`**. One day's schedule as JSON, grouped by room and by tutor, with cancellations, changes and flags. This is the minimum needed to see that the rules hold (`DECISIONS.md` §2). It is the data the Today board (phase 16) and the change badge (phase 17) will draw.

## In scope

- `FixedTimeProvider` and a `Clock:Now` config key.
- A pure builder that turns one day's sessions into the response. No I/O, no database.
- The endpoint.
- Unit tests for the builder, on the real export (through `SeedPlanner`, no database) and on small hand-made cases.
- Requests in `BrightPath.Api.http`.

## Out of scope

- Writes (create, cancel) → phases 11 and 13. They will read the same `TimeProvider`.
- The React grid → phase 16. The change badge in the UI → phase 17.
- A per-tutor day endpoint → phase 19. The `tutors` index here already gives a tutor's day inside one response.
- Rule breaks. They stay in `/api/reports/violations`. This endpoint only shows the `legacyViolation` flag the seed stored.
- An API-level test on a real database → phase 12.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Shape | One flat `sessions` list with full detail, plus `rooms` and `tutors` indexes that hold only session IDs. | "Grouped by room and tutor" without printing every session twice. The UI builds a room × time grid from `rooms`, and a tutor's day from `tutors`. |
| Cancelled | Included, with their status. Cancelled attendees too. | The board strikes them through (phase 16). Hiding them would hide "your lesson is gone", which is the change a tutor most needs to see. |
| Clock | `Clock:Now` in `appsettings.json`. When set, a `FixedTimeProvider` returns it. When the key is missing, `TimeProvider.System`. | Tests can move the clock by overriding one config key. Unpinning it for real use is a config change, not a code change. |
| Flags | Each session carries its changes (with `afterCutoff`), `legacyViolation`, and a `state` relative to now. Empty rooms (R4–R6) are listed. | The changes are the "changed after the tutor was told" record. `state` lets the board mark what is over. Empty rooms show where a lesson could still go. |

## Clock

- Config: `"Clock": { "Now": "2026-03-06T10:00:00+07:00" }` in `appsettings.json`.
- `FixedTimeProvider(DateTimeOffset now) : TimeProvider` overrides `GetUtcNow()` and returns `now` as UTC. It is registered as the `TimeProvider` singleton.
- The seed loader does not read the clock. Its times come from the CSV.
- The zone for local dates is still `BookingPolicy.TimeZone`, not the `TimeProvider`'s local zone.

## Endpoint

`GET /api/schedule?date=2026-03-06`

- `date` is an optional local date. Without it, the local date of the clock's now (2026-03-06).
- A session is on the day when its local start date is that date.
- Bad `date` (e.g. `not-a-date`) → **400** (binding, same as the report).
- **200** with:

```json
{
  "date": "2026-03-06",
  "now": "2026-03-06T10:00:00+07:00",
  "sessions": [
    {
      "id": "…",
      "tutorId": "T1",
      "tutorName": "…",
      "roomId": "R1",
      "startsAt": "2026-03-06T09:00:00+07:00",
      "endsAt": "2026-03-06T10:00:00+07:00",
      "durationMin": 60,
      "state": "past",
      "cancelled": false,
      "cancelledAt": null,
      "movedToSessionId": null,
      "legacyViolation": false,
      "changedAfterCutoff": false,
      "attendees": [
        {
          "id": "…",
          "studentId": "…",
          "studentName": "Vu Ha My",
          "lessonId": "L018",
          "status": "booked",
          "cancelledAt": null,
          "cancelledBy": null,
          "chargeable": false,
          "legacyViolation": false,
          "note": null
        }
      ],
      "changes": [
        {
          "kind": "cancelled",
          "attendeeId": "…",
          "changedAt": "…",
          "changedBy": "family",
          "afterCutoff": true,
          "note": null
        }
      ]
    }
  ],
  "rooms": [ { "id": "R1", "sessionIds": ["…"] } ],
  "tutors": [ { "id": "T1", "name": "…", "sessionIds": ["…"] } ]
}
```

### Fields

- **Times** are local, ISO-8601 with the offset (`+07:00`), as the tech stack says. `now` is too.
- **`state`**, compared with the clock, half-open like everything else: `past` when `endsAt <= now`, `in-progress` when `startsAt <= now < endsAt`, and `upcoming` when `now < startsAt`. It is about time only. A cancelled session still has a `state`, and the `cancelled` field says it is cancelled.
- **`changedAfterCutoff`** is true when any of the session's changes has `afterCutoff`. It is there so the badge (phase 17) does not have to scan the list.
- **`changes`** is every `booking_changes` row for the session, oldest first.
- **`lessonId`** is `source_lesson_id`, null for a booking made in the app.
- **`legacyViolation`** is on both the session and the attendee, because the seed sets them separately: a room or tutor overlap flags the session (L034), and a student overlap flags the attendee (L008).

### Order

- `sessions`: by `startsAt`, then `roomId`, then `id`.
- `attendees`: by `lessonId` (nulls last), then student name.
- `rooms`: **every** room in the `rooms` table, by id, including those with no session that day (empty `sessionIds`).
- `tutors`: **every** tutor, by id, including those with no session that day. This is the same rule as rooms, so a tutor's day off shows as an empty list.
- Each `sessionIds` list follows the `sessions` order. Cancelled sessions are in the lists too.

## Expected result on this export

`GET /api/schedule` (defaults to 2026-03-06, now 10:00):

| Index | Sessions (by lesson ID) |
|---|---|
| R1 | L018, L021, L022, L024, L025, L026, L027 |
| R2 | L019, L023 |
| R3 | L020 |
| R4, R5, R6 | empty |
| T1 | L018, L021, L022, L024, L025, L026, L027 |
| T2 | L019, L023 |
| T3 | L020 |

- 10 sessions. L018 and L019 (09:00–10:00) are `past`, because they end exactly at 10:00. The other 8 are `upcoming`. None is `in-progress`.
- Nothing is cancelled, nothing has a change, and nothing is `legacyViolation` on this date.

Other dates cover the other fields:

| Date | What it shows |
|---|---|
| 2026-03-03 | L005 is cancelled (session and attendee), `cancelledBy` `family`, `chargeable` false (5h45m before). It has one `cancelled` change, `afterCutoff` true, so `changedAfterCutoff` is true. Every session is `past`. |
| 2026-03-04 | The exam pair L009 + L010 is **one** session with two attendees. L007 and L008 are two sessions in R3 and R2. The L008 **attendee** is `legacyViolation` (a student overlap). Its session is not. |
| 2026-03-05 | L015 is `no_show`, and its session is not cancelled. L017 is cancelled by the `tutor`, `chargeable` false, with a change after the cut-off. |
| 2026-03-10 | The L034 **session** is `legacyViolation` (a tutor overlap). Its attendee is not. Every session is `upcoming`. |
| 2026-03-12 | No sessions. `rooms` and `tutors` are still listed, all empty. |

## Technical notes

- The builder (`Domain/ScheduleDay.cs`) takes the day's sessions with attendees and changes, the rooms, the tutors, `now`, and `BookingPolicy`. It returns the response. Keeping it pure means it can be tested on the planned seed, like `ScheduleRules`.
- The endpoint runs four `AsNoTracking` queries: the day's sessions (with attendees, students and tutor), the changes for those session IDs, rooms, and tutors. `BookingChange` has no navigation from `Session`, so the changes are a separate query and are grouped in memory.
- `BookingPolicy` gets a `ToLocal(instant)` helper that returns a `DateTimeOffset` in the centre's zone, for the response times.
- The endpoint is in its own `MapGroup("/api/schedule")`, with OpenAPI metadata.

## Docs

- `DECISIONS.md` §1 "Assumptions I had to invent": the pinned-today bullet says the clock is `Clock:Now` in config, and removing the key switches to the real time.
- `DECISIONS.md` §3 API table: the schedule row names the response shape (flat sessions plus room and tutor indexes, cancelled included, `state`, changes).
- `specs/tech-stack.md` "Clock" row: name the `Clock:Now` key.

## Context

- `DECISIONS.md` §1 (pinned today, times are local), §2 (read one day as JSON), §3 (data model, change records, API).
- Phase 9 spec: `specs/2026-09-27-violation-report/` (same pattern: a pure builder, tested on `SeedPlanner` output).
