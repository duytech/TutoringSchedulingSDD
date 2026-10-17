# Phase 27: One folder per web component — Plan

The tasks run in order, on their own branch, and the whole phase is one commit at the end. All paths are under `web/`.

1. **Before shots.** With the API and `npm run dev` running, take the browser screenshots listed in `validation.md`, in light and dark mode. Run `npm run build` and save the list of selectors in `dist/assets/*.css`. Both are the baseline for the checks at the end.

2. **Shared styles**
   - Create `src/styles/global.css` with `:root`, the dark-mode block, `*`, `body`, `.page`, `.message`, `.message--error` and `.message a`, in their `App.css` order.
   - `src/main.tsx`: `import './styles/global.css'` in place of `import './App.css'`, before `import App`.

3. **Move the components** (`git mv`, so `git log --follow` keeps the history)
   - `DayPage.tsx`, `TutorDayPage.tsx`, `ViolationsPage.tsx` → `src/pages/<Name>/<Name>.tsx`.
   - `PageHeader.tsx`, `DayGrid.tsx`, `SessionCard.tsx`, `TutorSheet.tsx` → `src/components/<Name>/<Name>.tsx`.

4. **Pull out the inner components**, each as a named export in its own folder, with its doc comment:
   - `LegacyRedirect` from `App.tsx` → `src/pages/LegacyRedirect/LegacyRedirect.tsx`.
   - `ApiErrorMessage` from `PageHeader.tsx` → `src/components/ApiErrorMessage/`.
   - `TutorLoads` from `DayPage.tsx` → `src/components/TutorLoads/`.
   - `RangeForm` and `ViolationList` from `ViolationsPage.tsx` → `src/components/RangeForm/` and `src/components/ViolationList/`. The `Range` type they share moves with `RangeForm`, and `ViolationsPage` imports it from there.

5. **Split the rest of `App.css`** into the component CSS files, following the component map in `requirements.md`. Keep the rule order and the section comments (`/* Session cards */`, …). The 640px media query becomes two: one in `PageHeader.css`, one in `ViolationList.css`. Components with no styles of their own get a CSS file with one comment. Each `.tsx` imports `./<Name>.css`. `ViolationsPage.tsx` also imports `../../components/PageHeader/PageHeader.css`. Delete `App.css`.

6. **Fix the imports.** Pages and components now reach the helpers through `../../api`, `../../dates`, etc. `App.tsx` imports the pages from `./pages/<Name>/<Name>`.

7. **Structure test**: `src/structure.test.ts`. It uses `import.meta.glob('./{pages,components}/*/*.{tsx,css}')`, so it needs no Node types. It asserts:
   - every folder under `pages/` and `components/` has `<Folder>.tsx`,
   - every folder has `<Folder>.css`, except `LegacyRedirect`,
   - no `.tsx` file is left directly in `src/` apart from `App.tsx` and `main.tsx`.

8. **`CLAUDE.md`**: add a `## Frontend` section with the rule: "Each component is a folder `<Name>/` with `<Name>.tsx` and `<Name>.css`. Routed pages go in `web/src/pages/`, the rest in `web/src/components/`. Shared styles live in `web/src/styles/global.css`."

9. **Build, lint, test, browser check** (see `validation.md`), then **commit** once the author asks:
   `refactor(web): give each component its own folder and stylesheet`.
