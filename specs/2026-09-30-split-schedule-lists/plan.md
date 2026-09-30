# Phase 21: Rooms and tutors as their own endpoints — Plan

The tasks run in order, and the whole phase is one commit at the end.

1. **Application**
   - `Schedule/ScheduleDay.cs`: drop `Rooms` and `Tutors` from `ScheduleDayView`, delete `RoomDay` and `TutorDay`, and drop the `roomIds` and `tutors` parameters of `Build`.
   - `Schedule/GetScheduleHandler.cs`: no longer needs `IReferenceData`.
   - `Rooms/GetRoomsHandler.cs` (`RoomView`) and `Tutors/GetTutorsHandler.cs` (`TutorView`): read `IReferenceData.RoomIdsAsync` / `TutorsAsync` and order by id, ordinal.
   - Register both in `AddApplication()`.

2. **Api**
   - `Endpoints/RoomEndpoints.cs`: `GET /api/rooms` (`GetRooms`). Mapped in `Program.cs`.
   - `Endpoints/TutorEndpoints.cs`: `GET /api/tutors` (`GetTutors`).
   - `Endpoints/ScheduleEndpoints.cs`: the summary and description no longer mention rooms and tutors.
   - `BrightPath.Api.http`: one request for each new endpoint.

3. **Web**
   - `api.ts`: `Room`, `Tutor`, `fetchRooms()`, `fetchTutors()`. `ScheduleDay` loses `rooms` and `tutors`.
   - `App.tsx`: the day view loads the day, the rooms and the tutors together. The tutor line counts each tutor's active sessions from `sessions`.
   - `DayGrid.tsx`: takes the room IDs as a prop.

4. **Tests**
   - `ScheduleDayTests`: the grouping checks read `sessions` by `RoomId` / `TutorId`.
   - New `ReferenceListHandlerTests`: both handlers order by id, with the in-memory fake.
   - Integration: the schedule tests read `sessions` instead of the indexes. New `ReferenceListEndpointTests`: `/api/rooms` gives R1–R6 and `/api/tutors` gives T1–T3.

5. **Docs**: `DECISIONS.md` API table, `README.md` curl sample.

6. **Build, test, run** (see `validation.md`), then **commit** once the author asks:
   `feat: serve rooms and tutors from their own endpoints`.
