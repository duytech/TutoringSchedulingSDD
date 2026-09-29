# Phase 14: README — Requirements

Roadmap: Stage C, phase 14 (est. 5 min). One atomic commit.

## Goal

The brief asks for "the commands to run it in your `README` with what you saw". The reader is a busy reviewer who was not in the room, and who will run the project on their own machine with their own Postgres. They should get from clone to a running API and a green `dotnet test` without asking anyone, and see in the README what the system did on the export.

The README replaces the current one-line stub. It is written in English.

## In scope

- `README.md` at the repo root.
- Running every command in it, in order, on a fresh database, to get the real output for "What I saw".

## Out of scope

- Design reasoning. That is `DECISIONS.md`, and the README links to it instead of repeating it.
- Reflection, next steps and weak spots → phase 15 (`DECISIONS.md` §4).
- PowerShell versions of the commands.
- Any code change. If a command in the README turns out not to work, the fix is its own commit, before this one.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| What I saw | **Real output, trimmed**, from one run on an empty database, pasted while the phase is built. Each block has one sentence on why it matters. | The reviewer can compare their own run against it. Output that was really seen is more convincing than a description. |
| Commands | **curl in bash** (macOS, Linux, Git Bash). One line points to the Scalar UI and `BrightPath.Api.http` for anyone who does not want a shell. | Works on most reviewers' machines and keeps the README short. |

## Sections, in order

The whole README fits in about two screens. Short sentences, code blocks for anything to type.

1. **Title and one paragraph.** What this is: the one feature picked from the brief (a conflict-safe schedule: book and cancel lessons without double-booking a student, room or tutor), built as an ASP.NET Core API on PostgreSQL. Links: `DECISIONS.md` for the why, `specs/` for the plan of each phase. One line: **today is pinned to Friday 2026-03-06 10:00 (+07:00)**.
2. **Prerequisites.**
   - .NET 10 SDK.
   - PostgreSQL 13 or later, on `localhost:5432`. `btree_gist` ships with the standard installers (contrib).
   - The DB user can `CREATE DATABASE` (the app creates `brightpath`, and each test run creates its own database) and `CREATE EXTENSION btree_gist` (a superuser, or the database owner, since `btree_gist` is a trusted extension from PG 13).
3. **Connection string.**
   - The default is in `src/BrightPath.Api/appsettings.Development.json`: `Host=localhost;Port=5432;Database=brightpath;Username=postgres;Password=postgres`.
   - If yours differs, set it without editing the file: `dotnet user-secrets set "ConnectionStrings:BrightPath" "…" --project src/BrightPath.Api`, or the `ConnectionStrings__BrightPath` environment variable.
   - The tests read the same setting and only replace the database name.
4. **Run.**
   - `dotnet run --project src/BrightPath.Api`. It listens on `http://localhost:5238`.
   - On first start it creates the database, applies the migrations and loads the export (`src/BrightPath.Api/Seed/`). If the database already has data, it does not load it again.
   - Start over: drop the database (`DROP DATABASE brightpath WITH (FORCE);` in `psql`) and run again.
   - Move the clock: `Clock__Now=2026-03-07T10:00:00+07:00 dotnet run --project src/BrightPath.Api`.
   - Scalar UI and the OpenAPI document: their URLs, checked when the phase is built.
5. **Test.**
   - `dotnet test`. What it needs (the same Postgres), and what it does: one throwaway database `brightpath_test_<guid>` per run, dropped at the end. The dev database is never touched.
   - What the tests cover, in one line each: the rules on the real export (unit), each conflict through HTTP, the two booking races and the cancel race, the pinned clock.
6. **Try it.** A short curl walk-through on a freshly seeded database, in this order:
   1. `GET /api/schedule`: today, the busiest day of the export.
   2. `GET /api/reports/violations`: the rules the export already broke.
   3. Take a student id from the schedule. Student ids are made at seed time, so the README shows how to read one: with `jq` if installed, otherwise from the Scalar UI.
   4. `POST /api/sessions` that is refused with three conflicts (T3, R3, 03-07 10:00, Tran Bao Long: row 5 of the phase 11 spec).
   5. `POST /api/sessions` that succeeds (T2, R4, 03-07 13:00, Vu Ha My).
   6. `POST …/cancel` on L020 (03-06 10:30) by `family`: chargeable, after the cut-off, and the session is cancelled.
7. **What I saw.** The real output of the steps above and of `dotnet test`, trimmed (see below).
8. **Troubleshooting.** Three lines:
   - `password authentication failed`: set the connection string (section 3).
   - `permission denied to create extension "btree_gist"`: use a superuser, or make the user the owner of the database.
   - A test run that was killed can leave a `brightpath_test_…` database behind. The query that lists them, and how to drop one.
9. **Where to read next.** `DECISIONS.md` (questions, contradictions, the pick, the design), `specs/roadmap.md` (phases in order), `specs/<date>-<phase>/` (one spec per phase), and the commit history (one commit per phase, what and why).

## What I saw

Captured from one run on an empty database, with the clock at the pinned default. Each item is a short output block (JSON trimmed with `…` where it is long, never edited) and one sentence.

| # | Output | The sentence says |
|---|---|---|
| 1 | The seed log line (tutors, students, sessions, attendees) | 34 CSV rows become fewer sessions, because the exam pair L009 + L010 is one session with two attendees. |
| 2 | The 4 violation report items, rule and message only | The export breaks the rules 4 times. It is loaded as history and flagged, not fixed or dropped. |
| 3 | The 409 with 3 conflicts | Every conflict comes back at once, in plain words, naming the lesson in the way. |
| 4 | The 201, trimmed to the new session's id, times and its `created` change | A new booking leaves a change record too. |
| 5 | The cancel's 200, trimmed to `chargeable`, the changes and `cancelled` | A late family cancel is charged, flagged after the cut-off, and frees the slot. |
| 6 | The `dotnet test` summary line | The number of tests that passed, and that it ran on its own database. |

The numbers in the table (34 rows, 4 items, 3 conflicts) are what the earlier phases saw. If the real run shows something else, the README shows what was really seen, and the difference is looked into before the commit.

## Context

- The brief, section 3 ("put the commands to run it in your `README` with what you saw").
- `specs/tech-stack.md`: Database setup, Tests row.
- Phase 11 spec (the 409 row), phase 13 spec (the cancel rows).
