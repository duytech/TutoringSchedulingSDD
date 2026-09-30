# Phase 20: Clean Architecture — Validation

The phase can be merged when every item below is true.

## Automated

- [ ] `dotnet build BrightPath.slnx` succeeds with zero warnings (the web project included).
- [ ] `dotnet test tests/BrightPath.Api.UnitTests` passes: the existing tests, `LayerTests` and `CreateSessionHandlerTests`.
- [ ] `SchemaTests` passes: the namespace move causes no pending model changes, and there is no new migration.
- [ ] `dotnet test tests/BrightPath.Api.IntegrationTests` passes, and `git diff tests/BrightPath.Api.IntegrationTests` shows only `using` lines. This includes `BookingRaceTests`, `MoveSessionTests` and the cancel race test, so the locks and transactions still behave the same.
- [ ] `LayerTests` fails when a Domain file adds `using Microsoft.EntityFrameworkCore;` and uses it (checked by hand, not committed).

## Structure

| # | Check | Expected |
|---|---|---|
| 1 | `BrightPath.Domain.csproj` | No `PackageReference` and no `ProjectReference` |
| 2 | `BrightPath.Application.csproj` | References Domain only. No EF Core or Npgsql package |
| 3 | `grep -r "BrightPathDbContext\|Npgsql" src/BrightPath.Api/Endpoints` | Nothing |
| 4 | The longest file in `src/BrightPath.Api/Endpoints` | Only routes, metadata and handler calls. No SQL and no validation |
| 5 | `dotnet ef migrations list --project src/BrightPath.Infrastructure --startup-project src/BrightPath.Api` | `InitialSchema` and `ExclusionConstraints`, both applied |
| 6 | `src/BrightPath.Api/bin/Debug/net10.0/Seed/` | `tutors.csv` and `lessons_export.csv` are there |

## Endpoint (curl or `.http`, on a freshly seeded dev DB, compared with `main`)

| # | Check | Expected |
|---|---|---|
| 1 | Startup | Migrates and seeds. `/health` returns `Healthy` |
| 2 | `GET /api/schedule`, `GET /api/tutors/T1/day`, `GET /api/reports/violations` | Same JSON as on `main` |
| 3 | `GET /api/reports/violations?from=2026-03-10&to=2026-03-01` | 400, title `Invalid date range` |
| 4 | `POST /api/sessions` valid / bad body / conflict | 201 with `Location` / 400 naming the fields / 409 with `conflicts` |
| 5 | Cancel and move requests from `BrightPath.Api.http` | Same status and JSON as on `main` |
| 6 | `/openapi/v1.json` | Same operations and schema names as on `main` |

## Browser (Playwright CLI, API and `npm run dev`)

| # | Check | Expected |
|---|---|---|
| 1 | The Today view on 03-06 | Renders the grid as before, with the cancelled lessons struck through |

## Docs and commit

- [ ] `specs/tech-stack.md`: the layout shows the 4 projects, and Clean Architecture is no longer under "Not used", with the reason.
- [ ] `README.md` has the new seed path and the `dotnet ef` flags. `TRANSACTIONS.md` links to the handlers.
- [ ] `BrightPath.slnx` lists the three new projects under `/src/`.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
- [ ] The dev DB is reseeded to the export afterwards.
