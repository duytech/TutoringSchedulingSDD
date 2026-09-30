# Phase 24: Each page loads its own data in a plain effect — Plan

The tasks run in order, and the whole phase is one commit at the end. All paths are under `web/src/`.

1. **`api.ts`**: `asApiError(cause: unknown): ApiError` returns `cause` when it is already an `ApiError`, and otherwise wraps `String(cause)` with no status.

2. **`DayPage.tsx`**
   - State:
     - `data` (`{ day, rooms, tutors }` or `null`)
     - `error` (`ApiError` or `null`)
     - `loadedDate` (`undefined` until the first answer)
   - `useEffect` on `[date]`:
     - Fetch the day, the rooms and the tutors together.
     - On success set `data`, clear `error` and set `loadedDate`. On failure set `error` and `loadedDate`.
     - Only while `current` is true. The cleanup sets it to false.
   - `loading = loadedDate !== date`.

3. **`TutorDayPage.tsx`**
   - The same shape: state `sheet data`, `error`, `loadedFor` (`{ tutor, date }`), and an effect on `[tutor, date]` that calls `fetchTutorDay`.
   - `loading` is true unless `loadedFor` matches both.
   - The `sheet.tutorId === tutor` guard stays.

4. Delete **`useLoad.ts`**.

5. **Build, lint, test, run** (see `validation.md`), then **commit** once the author asks:
   `refactor: load each page's data in its own effect`.
