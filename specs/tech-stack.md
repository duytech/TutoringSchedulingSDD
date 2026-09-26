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
| Frontend | **React + Vite + TypeScript** | A single Today view page. The Vite dev proxy calls the API, so no CORS setup is needed. |
| Tests | **xUnit + WebApplicationFactory** against the local Postgres | End-to-end tests against a real Postgres. Each test run creates a throwaway database (`brightpath_test_<guid>`), migrates it, and drops it afterwards. Each business rule has one test that proves it is enforced. |
| Clock | `TimeProvider` (fixed, from config) | Today is pinned to 2026-03-06 (+07:00). Tests can move the clock. |
| Local infra | None. Uses the PostgreSQL already installed on the machine | No Docker. The API runs with `dotnet run`. It applies migrations and seeds on startup when the DB is empty. |

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
/seed/                      ← lessons_export.csv, tutors.csv (copied from .assignment)
/src/BrightPath.Api/        ← Minimal API, EF Core, seed loader
/tests/BrightPath.Api.Tests/← integration tests
/web/                       ← React + Vite Today view
```

## Where each rule is enforced

The brief asks us to explain this split, so it is decided up front.

| Rule | Where | Mechanism |
|---|---|---|
| Room holds one session at a time | **DB** | `EXCLUDE USING gist (room_id WITH =, slot WITH &&) WHERE (active)` |
| Tutor in one place at a time | **DB** | `EXCLUDE USING gist (tutor_id WITH =, slot WITH &&) WHERE (active)` |
| Student in one place at a time | **DB** | `slot` is copied onto the attendee row, then `EXCLUDE (student_id WITH =, slot WITH &&) WHERE (active)` |
| Duration is 60 or 90 | **DB** | `CHECK` |
| Status values | **DB** | `CHECK` / enum |
| Max 2 attendees per session | **Code** (+ DB trigger if time allows) | Validated in the create/add-attendee handler |
| Max 6 sessions per tutor per day | **Code** | Counts across rows. Runs inside a transaction with a per-tutor-day advisory lock to avoid races. |
| Closed Monday, opening hours | **Code** | Configurable policy. Kept out of `CHECK` so the seeded history can still load. |
| Late cancellation (< 4h) → chargeable | **Code** | Computed when the cancel happens and stored on the row |
| Cut-off (16:00 day before) → change is flagged | **Code** | Computed when the change is written and stored in `booking_changes.after_cutoff` |

**Why this split:** anything that must hold even when two receptionists click at the same moment (overlaps) goes in the DB. Policy numbers the owner may change (6/day, 4h, 16:00, opening hours) go in code and config.

**Seed history vs. constraints:** imported rows that break a rule are marked `legacy_violation = true` and left out of the exclusion constraints through the `WHERE` predicate. New writes are still checked in code against **all** active rows, including legacy ones, so the system never adds a new conflict on top of an old one.

## Conventions

- Nullable reference types are on, warnings are treated as errors, and file-scoped namespaces are used.
- Time handling:
  - Store `timestamptz`.
  - Compute business logic in `Asia/Ho_Chi_Minh`.
  - Show local times in the API (ISO-8601 with offset).
- Names follow the domain: `Session`, `Attendee`, `BookingChange`, `Room`, `Tutor`, `Student`. There are no generic `Manager` or `Helper` classes.
- Nothing is ever deleted. Status transitions happen only through endpoints.
- Commits are atomic, and each message says **what + why** in Conventional Commits style (`feat:`, `docs:`, `test:`, `chore:`).

## Not used (and why)

- **MediatR, CQRS, Clean Architecture layers:** too heavy for a single feature built in 2.5h.
- **SQLite:** it has no range exclusion constraints, so we would lose the DB guarantee.
- **SQL Server:** it would need triggers or serializable transactions for overlap checks.
- **Docker / Testcontainers:** the machine already runs PostgreSQL locally, so a container would only add setup. Trade-off: a reviewer has to point the connection string at their own Postgres.
- **Angular:** too heavy for one screen.
- **FluentAssertions:** its licence changed. We use plain xUnit `Assert`.
