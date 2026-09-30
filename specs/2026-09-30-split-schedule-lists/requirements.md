# Phase 21: Rooms and tutors as their own endpoints — Requirements

Roadmap: not on the roadmap. A small API change that comes after phase 20. It is one atomic commit.

## Goal

`GET /api/schedule` returns `sessions` plus two indexes, `rooms` and `tutors`, each holding the IDs of that day's sessions. Rooms and tutors are reference data, not part of a day, and the indexes repeat what `sessions` already says (each session has a `roomId` and a `tutorId`).

After this phase:

| Endpoint | Returns |
|---|---|
| `GET /api/rooms` | Every room, ordered by id: `[{ "id": "R1" }, …]` |
| `GET /api/tutors` | Every tutor, ordered by id: `[{ "id": "T1", "name": "…", "subject": "…" }, …]` |
| `GET /api/schedule?date=` | `{ date, now, sessions }`. No `rooms` and no `tutors` |

## In scope

- Two new read handlers in Application, two new routes in Api.
- Removing `rooms`, `tutors`, `RoomDay` and `TutorDay` from the schedule.
- The web app reads the room columns and the tutor line from the new endpoints, and counts a tutor's sessions from `sessions`.
- Tests and docs in the same commit.

## Out of scope

- Any change to `sessions`, to `GET /api/tutors/{id}/day`, or to the write endpoints.
- A date filter on the new endpoints: rooms and tutors do not change by day.
- Any schema change.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Shape | Plain reference lists, no `sessionIds`. | The per-day index can be worked out from `sessions`. Keeping it would be the same data twice. |
| Compatibility | `rooms` and `tutors` are removed from the schedule, not kept alongside. | The only client is our web app, changed in the same commit. |
| Order | By id, ordinal (`R1`…`R6`, `T1`…`T3`), as the schedule did. | The grid columns and the tutor line keep their order. |
