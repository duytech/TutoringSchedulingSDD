# Phase 26: Container delivery — Requirements

Roadmap: not on the roadmap. It comes after phase 25 and after CI (`.github/workflows/ci.yml`, which builds and tests on every push and pull request). It is one atomic commit, delivered through a pull request.

## Goal

To run the app today, a reviewer needs .NET 10, Node and a local PostgreSQL with `btree_gist`, plus a connection string. After this phase they need only Docker:

```bash
docker compose up --build
# http://localhost:8080/rooms
```

Every commit on `main` that passes CI is also built into images and pushed to the GitHub Container Registry (continuous delivery). The `latest` images can be pulled instead of built.

**No app code changes.** The C# and TypeScript stay as they are. Everything is new config files, a script and docs.

## In scope

| File | Change |
|---|---|
| `src/BrightPath.Api/Dockerfile` | The API image: `dotnet publish` on the .NET 10 SDK, run on `aspnet:10.0` as a non-root user. |
| `web/Dockerfile` | The web image: `npm ci` + `npm run build` on Node 22, served by nginx. |
| `web/nginx.conf` | Serves `dist`, forwards `/api/` to the API, and falls back to `index.html` for the page routes. |
| `.dockerignore`, `web/.dockerignore` | Keep build output, `node_modules` and local folders out of the build context. |
| `compose.yaml` | `db` + `api` + `web`. Only `web` is published, on port 8080. |
| `scripts/smoke-test.sh` | Checks a running stack through port 8080. Used by CI and by hand. |
| `.github/workflows/ci.yml` | A third job, `container`: build the stack, run the smoke test. Runs on pull requests too. |
| `.github/workflows/cd.yml` | After CI passes on `main`: build both images and push them to `ghcr.io`. |
| `README.md` | A "Run with Docker" section, and one line about CD. |
| `specs/tech-stack.md` | "Local infra" and the Docker entry in "Not used" change (see Decisions). |
| `DECISIONS.md` | One bullet in §4 "Where it stopped" for CI and container delivery. |

## Out of scope

- Deploying to a host (Render, Azure, a VPS). No account or secret is needed for this phase.
- Any change to the API, the web code or their tests.
- Docker for development. Dev stays on `dotnet run`, `npm run dev` and the local Postgres.
- Running the .NET tests in a container. CI's `api` job keeps doing that.
- HTTPS, a domain, or production secrets. The compose file is for trying the app on one machine.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision | Why |
|---|---|---|
| Role of Docker | A second way to run the app, and the format CD delivers. Dev is unchanged. | The tech stack chose "no Docker" so dev needs no extra setup. That still holds. Docker is added for the reviewer, who otherwise has to install and configure Postgres. |
| Containers | `web` (nginx) → `api` → `db` (postgres:17). | The web calls `/api/...` on its own origin. In dev the Vite proxy forwards it. nginx does the same job in the container, so neither the API nor the web changes. |
| Database | No published port and no volume. | Port 5432 is already used by the local Postgres. With no volume, `docker compose down` and `up` give a freshly seeded week every time, which is what a reviewer wants. |
| Images | `ghcr.io/duytech/tutoringschedulingsdd-api` and `-web`, tagged with the commit SHA and `latest`. | Free for a public repo, and the push uses the workflow's own `GITHUB_TOKEN`. The SHA says exactly which commit is running and allows a rollback. GHCR names must be lower case. |
| When CD runs | `workflow_run` after CI completes on `main`, only on success. Also `workflow_dispatch`. | A commit that fails CI is never delivered. |
| Smoke test | One script, run by CI's `container` job on every pull request. | A broken Dockerfile or nginx config is caught before merge, not after. The same script runs locally. |
| Delivery | One commit on branch `cd/ghcr-images`, then a pull request. | So the new `container` job runs before the change reaches `main`. |

## Containers

```
browser ──:8080──▶ web (nginx)
                    ├─ /api/*  ──▶ api:8080 (ASP.NET Core, Production) ──▶ db:5432 (postgres:17)
                    └─ /*      ──▶ dist/, else index.html
```

- `api` gets `ConnectionStrings__BrightPath=Host=db;Port=5432;Database=brightpath;Username=postgres;Password=postgres`. Everything else comes from `appsettings.json`: the booking policy, the pinned clock (2026-03-06 10:00 +07:00) and the seed. On start it creates the database, migrates and loads the export, as it does today.
- `api` waits for `db` to be healthy (`pg_isready`). `web` starts after `api`.
- Production environment, so the OpenAPI document and Scalar UI are off, as `Program.cs` already decides.

## Smoke test (`scripts/smoke-test.sh [base-url]`)

The base URL defaults to `http://localhost:8080`. The script stops at the first failed check and exits non-zero.

| # | Request | Expected | Proves |
|---|---|---|---|
| 1 | `GET /api/rooms`, retried for up to 90 s | 200, body contains `"R6"` | nginx → API → Postgres works, and migrations ran |
| 2 | `GET /api/sessions?date=2026-03-06` | 200, body contains `"lessonId":"L018"` | The export was seeded, and the centre's time zone resolves in the image |
| 3 | `GET /tutors/T1` | 200, body contains `<div id="root">` | A page route falls back to `index.html` |
| 4 | `GET /api/does-not-exist` | 404 | An unknown API path is the API's 404, not the page |

## Package visibility

None needed from the owner. The `org.opencontainers.image.source` label in both Dockerfiles links each package to this public repo, and GitHub makes the package public with it. This was confirmed after the first CD run: both images were pulled anonymously.

## Context

- Why no Docker until now: `specs/tech-stack.md`, "Not used".
- How the API starts (migrate + seed): `src/BrightPath.Infrastructure/DependencyInjection.cs`.
- The dev proxy that nginx replaces: `web/vite.config.ts`.
