# Phase 24: Each page loads its own data in a plain effect — Requirements

Roadmap: not on the roadmap. A web-only refactor that comes after phase 23. It is one atomic commit.

## Goal

`DayPage` and `TutorDayPage` load their data through a shared hook, `useLoad(key, load)`. The hook needs a string key, a `load` wrapped in `useCallback`, and it works out `loading` for you. To see what a page fetches, when it fetches again, and what happens to a late answer, you have to read the hook as well as the page.

After this phase each page writes its own `useState` + `useEffect`, so the whole flow reads top to bottom in the component, and `useLoad.ts` is gone.

## In scope

- `DayPage.tsx` and `TutorDayPage.tsx` each hold their data, error and "loaded for" state, and fetch in their own `useEffect`.
- A small `asApiError(cause)` in `api.ts`, so both pages wrap an unexpected error the same way.
- Deleting `useLoad.ts`.

## Out of scope

- Any change to what the pages show, the routes, the API or the CSS.
- A data-fetching library (TanStack Query, SWR, route loaders).

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Where the loading lives | In each page's own `useEffect`, not in a shared hook. | The owner wants it explicit. With two pages the duplication is small. |
| Late answers | Each effect keeps its `let current = true` flag and its cleanup. | Stepping through days or tutors quickly must not show an old answer. |
| `loading` | Derived: the page records what it last loaded for (the date, or the tutor and the date) and compares it with what the URL asks for. | State is only set in the promise callbacks, so there is no synchronous `setState` in an effect (react-hooks v7 lint). |
| While loading, after an error | The previous data stays on screen. | Same as today: the page does not flash. |
