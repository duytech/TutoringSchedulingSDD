# Phase 27: One folder per web component — Requirements

Roadmap: not on the roadmap. A web-only refactor that comes after phase 26. It is one atomic commit.

## Goal

The web app keeps 13 components in 8 flat `.tsx` files under `web/src/`. Five of them (`LegacyRedirect`, `ApiErrorMessage`, `TutorLoads`, `RangeForm`, `ViolationList`) hide inside another component's file. All their styles live in one 397-line `App.css`, so a change to a card means searching a file that also holds the grid, the header and the violations list.

After this phase each component is a folder that holds its own `.tsx` and its own `.css`:

```
web/src/
  main.tsx                 imports styles/global.css, then App
  App.tsx                  routes only
  styles/
    global.css             tokens, reset, and the classes every page shares
  pages/
    DayPage/               DayPage.tsx, DayPage.css
    TutorDayPage/          TutorDayPage.tsx, TutorDayPage.css
    ViolationsPage/        ViolationsPage.tsx, ViolationsPage.css
    LegacyRedirect/        LegacyRedirect.tsx
  components/
    PageHeader/            PageHeader.tsx, PageHeader.css
    ApiErrorMessage/       ApiErrorMessage.tsx, ApiErrorMessage.css
    DayGrid/               DayGrid.tsx, DayGrid.css
    SessionCard/           SessionCard.tsx, SessionCard.css
    TutorSheet/            TutorSheet.tsx, TutorSheet.css
    TutorLoads/            TutorLoads.tsx, TutorLoads.css
    RangeForm/             RangeForm.tsx, RangeForm.css
    ViolationList/         ViolationList.tsx, ViolationList.css
  api.ts, colours.ts, dates.ts, labels.ts, layout.ts, sessionStatus.ts,
  tutorChanges.ts, useDate.ts, violations.ts, and their *.test.ts   (unchanged)
```

Nothing changes for the user: same pages, same class names, same look in light and dark mode.

## Component map

| Component | Today | Goes to | Its CSS, moved from `App.css` |
|---|---|---|---|
| `DayPage` | `DayPage.tsx` | `pages/DayPage/` | None of its own. The file holds one comment: it uses `.page` and `.message` from `global.css`. |
| `TutorDayPage` | `TutorDayPage.tsx` | `pages/TutorDayPage/` | None of its own (same comment). |
| `ViolationsPage` | `ViolationsPage.tsx` | `pages/ViolationsPage/` | None of its own. It also imports `PageHeader.css` (see below). |
| `LegacyRedirect` | inside `App.tsx` | `pages/LegacyRedirect/` | Exempt: it only redirects and renders no markup. |
| `PageHeader` | `PageHeader.tsx` | `components/PageHeader/` | `.top`, `.top h1`, `.top__sub`, `.top__nav`, `.top__nav button`, `.top a`, and the `.top` rule of the 640px media query |
| `ApiErrorMessage` | inside `PageHeader.tsx` | `components/ApiErrorMessage/` | None of its own (uses `.message`, `.message--error`). |
| `DayGrid` | `DayGrid.tsx` | `components/DayGrid/` | `.grid`, `.grid__corner`, `.grid__room`, `.grid__line`, `.grid__line--hour`, `.grid__time`, `.grid__now` |
| `SessionCard` | `SessionCard.tsx` | `components/SessionCard/` | `.card` and every `.card__*`, `button.card__moved`, `.student--cancelled`, `.card--row` (and its two descendant rules), `.card--past`, `.card--in-progress`, `.card--cancelled` (and its descendant rule) |
| `TutorSheet` | `TutorSheet.tsx` | `components/TutorSheet/` | `.changes`, `.changes h2`, `.changes ul`, `.sheet` |
| `TutorLoads` | inside `DayPage.tsx` | `components/TutorLoads/` | None of its own (links take the colour from `.top a`). |
| `RangeForm` | inside `ViolationsPage.tsx` | `components/RangeForm/` | `.range`, `.range input`, `.range button`, `.range button:disabled` |
| `ViolationList` | inside `ViolationsPage.tsx` | `components/ViolationList/` | `.violations`, `.violations h2`, `.violations ul`, `.violation`, `.violation__rule`, `.violation__message`, `.violation__lessons`, and the `.violation` rule of the 640px media query |

`App` and `main.tsx` stay in `src/`: they are the entry points, not reusable pieces.

## Shared styles

`src/styles/global.css` holds what no single component owns:

- the `:root` tokens and the dark-mode `@media (prefers-color-scheme: dark)` block,
- `*` and `body`,
- `.page` (used by all three pages), `.message`, `.message--error` (used by the pages, `ApiErrorMessage` and `TutorSheet`), and `.message a`.

`main.tsx` imports it once, in place of `import './App.css'`, before `App`. The combined rule `.top a, .message a` is split: `.top a` goes to `PageHeader.css`, `.message a` to `global.css`.

## Rules

1. Each component file is `<Name>/<Name>.tsx` and imports `./<Name>.css` (except `LegacyRedirect`).
2. A component with no styles of its own still has `<Name>.css`, holding one comment that names the shared classes it uses. When it gains styles, they have an obvious place.
3. Inside every CSS file the rules keep the order they had in `App.css`, so the cascade does not change.
4. No class is renamed, and no markup, prop or behaviour changes.
5. Imports point at the file, with no `index.ts` barrel: `import { SessionCard } from '../../components/SessionCard/SessionCard'`.
6. `App.css` is deleted.

## ViolationsPage and the header classes

`ViolationsPage` writes its own `<header className="top">` instead of using `PageHeader`, because its header holds the range form instead of the day arrows. So it imports `../../components/PageHeader/PageHeader.css` itself. The dependency then shows in the code, instead of working only because another page loaded the file. Making it use `PageHeader` is out of scope.

## In scope

- Moving the 8 component files and pulling the 5 inner components out into their own folders.
- Splitting `App.css` into `global.css` and the component CSS files.
- Fixing every import path.
- One test that checks the folder rule (`structure.test.ts`).
- A frontend rule in `CLAUDE.md`.

## Out of scope

- CSS Modules or any other change to how class names work.
- Making `ViolationsPage` use `PageHeader`.
- Moving the helper modules (`api.ts`, `dates.ts`, …) or their tests.
- New component tests, and any visual change.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Layout | `src/pages/` for routed pages, `src/components/` for the rest. No `index.ts` barrels. | A reader sees at once which components own a URL. Direct imports keep "go to definition" one hop and add no extra files. |
| CSS | Plain CSS per component, BEM class names kept. | The look stays exactly the same and the diff stays a move. CSS Modules would rewrite every `className`. |
| Inner components | Every component gets its own folder, including the private ones. `LegacyRedirect` is the one exemption from the `.css` file. | One rule with no judgement calls. `LegacyRedirect` renders nothing, so a stylesheet would be noise. |
| Helper modules | Stay in `src/`. | They are not components. Moving them would double the diff for no gain in this phase. |
