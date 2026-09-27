# Phase 6: Schema, core tables — Requirements

Roadmap: Stage B, phase 6 (est. 10 min). One atomic commit.

## Goal

Turn the data model in `DECISIONS.md` §3 into EF Core entities and one migration, so phases 7 (exclusion constraints) and 8 (seed loader) have tables to build on. The database already refuses bad values (wrong duration, unknown status) through `CHECK` constraints.

## In scope

- Entities: `Tutor`, `Room`, `Student`, `Session`, `Attendee`, `BookingChange`.
- `BrightPathDbContext` with a `DbSet` for each, and configuration kept next to the entities (`IEntityTypeConfiguration<T>`).
- snake_case table and column names through `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()`).
- `CHECK` constraints for durations and enum-like values (listed below).
- Rooms `R1`–`R6` seeded as reference data through `HasData`, so they exist after `MigrateAsync` with no loader.
- A local `dotnet-ef` tool manifest (`dotnet-tools.json` at the repo root), so a reviewer can run migrations without a global install.
- One migration: `InitialSchema`.
- One test: the EF model has no changes that are missing from the migrations.
- Roadmap fix: phase 7 says `tsrange`, which becomes `tstzrange` because the columns are `timestamptz`.

## Out of scope (later phases)

- `slot` range column, `btree_gist`, `EXCLUDE` constraints → phase 7.
- Importing the CSVs and tutors → phase 8. Tutors are **not** in `HasData`, because they come from `tutors.csv`.
- Endpoints, rule checks in code, `TimeProvider` → phases 10+.
- Throwaway test database → phase 12.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Primary keys | `uuid` (Guid, generated client-side as v7) for `students`, `sessions`, `attendees`, `booking_changes`. `tutors.id` and `rooms.id` stay as text codes (`T1`, `R1`). | Guids can be created in code before insert, so a session and its attendees can be built in one go. Tutor and room codes are the natural IDs that people use. |
| Trace to CSV | `attendees.source_lesson_id` (text, nullable, unique): `L001`… | One CSV row is one attendee. The violation report (phase 9) has to name the original lesson IDs. |
| `slot` column | Deferred to phase 7 as a generated `tstzrange`. | Keeps phase 6 pure EF. All range and GiST work lives in one raw-SQL migration. |
| Naming | `EFCore.NamingConventions`, snake_case. | One line instead of mapping every column. The raw SQL in phase 7 matches the names. |
| Enum-like values | Stored as `text` with a `CHECK`, not as Postgres enums. | Easy to read in psql, and adding a value later is a `CHECK` change, not an `ALTER TYPE`. |
| Deletes | Every FK is `ON DELETE RESTRICT`. | Nothing is ever deleted (tech-stack conventions). |
| Validation | A no-pending-model-changes test plus manual psql checks. No test writes to the dev database. | The throwaway test DB is phase 12. Writing test rows into the dev DB now would break the seed later. |

## Tables

Types are PostgreSQL. `timestamptz` everywhere for time.

### `tutors`
| Column | Type | Notes |
|---|---|---|
| `id` | text PK | `T1`… |
| `name` | text not null | |
| `subject` | text not null | |

`phone` from `tutors.csv` is not stored. The feature does not need it.

### `rooms`
| Column | Type | Notes |
|---|---|---|
| `id` | text PK | `R1`…`R6`, seeded with `HasData` |

### `students`
| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `name` | text not null, **unique** | The name is the identity (DECISIONS §1 assumption). |

### `sessions`
| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `tutor_id` | text FK → tutors | |
| `room_id` | text FK → rooms | |
| `starts_at` | timestamptz not null | |
| `ends_at` | timestamptz not null | |
| `cancelled_at` | timestamptz null | Active while null. |
| `moved_to_session_id` | uuid null FK → sessions | Used by the stretch "move" phase. |
| `legacy_violation` | bool not null default false | |

CHECK `ck_sessions_duration`: `ends_at - starts_at IN (interval '60 minutes', interval '90 minutes')`.

### `attendees`
| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `session_id` | uuid FK → sessions | |
| `student_id` | uuid FK → students | |
| `status` | text not null | `booked`, `cancelled`, `no_show` |
| `cancelled_at` | timestamptz null | |
| `cancelled_by` | text null | `family`, `tutor`, `centre` |
| `chargeable` | bool not null default false | |
| `legacy_violation` | bool not null default false | |
| `source_lesson_id` | text null, unique | `L001`… for seeded rows |
| `note` | text null | Free text from the CSV. |

- CHECK `ck_attendees_status`: status in the list above.
- CHECK `ck_attendees_cancelled_by`: `cancelled_by IS NULL OR` in the list above.
- CHECK `ck_attendees_cancelled_consistent`: `(status = 'cancelled') = (cancelled_at IS NOT NULL)`.
- Unique `(session_id, student_id)`: one student cannot be in the same session twice.

`cancelled_by` stays nullable even when cancelled: the seed has to infer it from the note, and that is phase 8's call.

### `booking_changes`
| Column | Type | Notes |
|---|---|---|
| `id` | uuid PK | |
| `session_id` | uuid FK → sessions | |
| `attendee_id` | uuid null FK → attendees | Null for a session-level change. |
| `kind` | text not null | `created`, `cancelled`, `moved` |
| `changed_at` | timestamptz not null | |
| `changed_by` | text null | `family`, `tutor`, `centre` |
| `after_cutoff` | bool not null | |
| `note` | text null | |

- CHECK `ck_booking_changes_kind` and `ck_booking_changes_changed_by` (null allowed).
- Index on `(session_id, changed_at)` for the tutor-day read later.

## Context

- `specs/mission.md`: "Changes after the cut-off" and the key interpretations table.
- `specs/tech-stack.md`: "Where each rule is enforced" and "Conventions".
- `DECISIONS.md` §3 "Data model". This phase adds `source_lesson_id`, `note` and uuid keys. §3 is updated in the **same commit** (roadmap definition of done).
