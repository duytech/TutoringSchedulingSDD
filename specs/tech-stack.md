# Tech Stack

Chosen for speed of delivery by a senior .NET developer, and so the **database itself** can enforce the "never double-book" rule.

## Overview

| Layer | Choice | Why |
|---|---|---|
| Runtime | **.NET 10 (LTS)** | Current LTS. What I am quickest in. |
| API | **ASP.NET Core Minimal API** | Little boilerplate. Endpoints are grouped by feature (`MapGroup`). |
| Errors | RFC 7807 `ProblemDetails` | A rule violation returns **409 Conflict** with a machine-readable list of conflicts. Bad input returns 400. |
| API docs | Built-in OpenAPI + Scalar UI, plus `.http` files | Reviewers can call the API without extra tools. |
| ORM | **EF Core 10 + Npgsql** | Migrations and LINQ for reads. Raw SQL in a migration for constraints EF cannot model. |
| Database | **PostgreSQL (local install, localhost:5432)** | `btree_gist` **exclusion constraints** on time ranges stop overlaps at the DB level, even under races. |
| Frontend | **React + Vite + TypeScript**, Vitest | A single Today view page. The Vite dev proxy calls the API, so no CORS setup is needed. Vitest tests the pure grid layout. It is in the solution as `web/BrightPath.Web.esproj` (JavaScript SDK), so building the solution also runs the npm build. |
| Tests | **xUnit + WebApplicationFactory** against the local Postgres | End-to-end tests against a real Postgres. Each test run creates one throwaway database (`brightpath_test_<guid>`), migrates and seeds it, and drops it afterwards. The API tests share it, each booking on a date of its own. Each business rule has one test that proves it is enforced. Races are forced by holding a transaction open in the test, not by parallel requests, so a missing guard fails the test. |
| Clock | `TimeProvider` (fixed, from `Clock:Now` in config) | Today is pinned to 2026-03-06 10:00 (+07:00). Tests and runs can move the clock by overriding `Clock:Now` (e.g. `Clock__Now`). Without the key, the real time is used. |
| Local infra | The PostgreSQL already installed on the machine, for dev and tests. Docker Compose only to try the app (phase 26) | Dev needs no Docker: the API runs with `dotnet run`, and it applies migrations and seeds on startup when the DB is empty. `compose.yaml` runs Postgres, the API and nginx for a reviewer who has only Docker. CD pushes those images to GHCR. |

## Database setup

- **Prerequisite:** PostgreSQL running on `localhost:5432`. The `btree_gist` extension ships with the standard installers (it is in contrib).
- **Connection string:** `ConnectionStrings:BrightPath` in `appsettings.Development.json`, which points at the local defaults (`Host=localhost;Port=5432;Database=brightpath;Username=postgres;Password=postgres`). It can be overridden with `dotnet user-secrets` or the `ConnectionStrings__BrightPath` environment variable, so real passwords are never committed.
- **Permissions:** the DB user must be allowed to `CREATE DATABASE` (for the test databases) and `CREATE EXTENSION btree_gist` (superuser, or the database owner on PG 13+).
- The README lists these prerequisites, because a reviewer has to configure their own Postgres.

## Repository layout

```
/DECISIONS.md               ← deliverable (phases 1–4 of the brief)
/README.md                  ← prerequisites, run commands + what I saw
/specs/                     ← this constitution
/src/BrightPath.Domain/          ← entities and centre rules, no package references
/src/BrightPath.Application/     ← one handler per use case, and the ports it uses
/src/BrightPath.Infrastructure/  ← EF Core + Npgsql: DbContext, migrations, ports, locks, seed loader
/src/BrightPath.Infrastructure/Seed/ ← lessons_export.csv, tutors.csv (copied from .assignment), seed loader
/src/BrightPath.Api/             ← Minimal API host: Program.cs, thin endpoints, Result → HTTP
/tests/BrightPath.Api.UnitTests/        ← unit tests, no database
/tests/BrightPath.Api.IntegrationTests/ ← integration tests, throwaway database
/web/                       ← React + Vite Today view
```

## Where each rule is enforced

The brief asks us to explain this split, so it is decided up front.

| Rule | Where | Mechanism |
|---|---|---|
| Room holds one session at a time | **DB** | `ex_sessions_room_slot`: `EXCLUDE USING gist (room_id WITH =, slot WITH &&) WHERE (cancelled_at IS NULL AND NOT legacy_violation)` |
| Tutor in one place at a time | **DB** | `ex_sessions_tutor_slot`: `EXCLUDE USING gist (tutor_id WITH =, slot WITH &&) WHERE (cancelled_at IS NULL AND NOT legacy_violation)` |
| Student in one place at a time | **DB** | A `BEFORE INSERT` trigger (`trg_attendees_copy_slot`) copies `sessions.slot` onto the attendee row, then `ex_attendees_student_slot`: `EXCLUDE USING gist (student_id WITH =, slot WITH &&) WHERE (status <> 'cancelled' AND NOT legacy_violation)` |
| Duration is 60 or 90 | **DB** | `CHECK` |
| Status values | **DB** | `CHECK` / enum |
| Max 2 attendees per session | **Code** (+ DB trigger if time allows) | Validated in the create/add-attendee handler |
| Max 6 sessions per tutor per day | **Code** | Counts across rows. Runs inside a transaction with a per-tutor-day advisory lock to avoid races. |
| Closed Monday, opening hours | **Code** | Configurable policy. Kept out of `CHECK` so the seeded history can still load. |
| Late cancellation (< 4h) → chargeable | **Code** | Computed when the cancel happens and stored on the row |
| Cut-off (16:00 day before) → change is flagged | **Code** | Computed when the change is written and stored in `booking_changes.after_cutoff` |

**Why this split:** anything that must hold even when two receptionists click at the same moment (overlaps) goes in the DB. Policy numbers the owner may change (6/day, 4h, 16:00, opening hours) go in code and config.

**Seed history vs. constraints:** for each overlapping pair in the import (L007/L008, L033/L034), only the **later** row is marked `legacy_violation = true` and left out of the exclusion constraints through the `WHERE` predicate. The earlier row stays covered, so the DB still guards that slot against new writes. The seed loader computes these flags with the same overlap rules, and the constraints check them on insert: a missing flag would make the seed fail. Rules checked only in code (Monday, hours, 6/day) need no flag, because they never block the import. Every seeded rule break is listed by the violation report.

## Conventions

- Nullable reference types are on, warnings are treated as errors, and file-scoped namespaces are used.
- Time handling:
  - Store `timestamptz`.
  - Compute business logic in `Asia/Ho_Chi_Minh`.
  - Show local times in the API (ISO-8601 with offset).
- Names follow the domain: `Session`, `Attendee`, `BookingChange`, `Room`, `Tutor`, `Student`. There are no generic `Manager` or `Helper` classes.
- **Clean Architecture layers (phase 20):** Api → Infrastructure → Application → Domain, each a project. They were first left out as too heavy for a 2.5h build. They were added once the endpoints had grown to 500 lines mixing HTTP, SQL locks and rules, and a use case could only be tested through HTTP. `LayerTests` keeps the dependencies pointing inward.
- Nothing is ever deleted. Status transitions happen only through endpoints.
- Commits are atomic, and each message says **what + why** in Conventional Commits style (`feat:`, `docs:`, `test:`, `chore:`).

## Not used (and why)

- **MediatR, CQRS:** too heavy for 7 use cases. A use case is a plain handler class, injected into its endpoint.
- **SQLite:** it has no range exclusion constraints, so we would lose the DB guarantee.
- **SQL Server:** it would need triggers or serializable transactions for overlap checks.
- **Docker for dev / Testcontainers:** the machine already runs PostgreSQL locally, so a container would only add setup to the dev loop and the tests. A reviewer who does not want to set up Postgres can use `docker compose up` instead (phase 26).
- **Angular:** too heavy for one screen.
- **FluentAssertions:** its licence changed. We use plain xUnit `Assert`.
