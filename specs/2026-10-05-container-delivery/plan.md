# Phase 26: Container delivery — Plan

The tasks run in order, and the whole phase is one commit at the end, on branch `cd/ghcr-images`.

1. **API image** (`src/BrightPath.Api/Dockerfile`, build context = repo root)
   - Stage `build`: `mcr.microsoft.com/dotnet/sdk:10.0`. Copy `Directory.Build.props` and `src/`, then `dotnet publish src/BrightPath.Api -c Release -o /app`. Only the API project, so the web's `.esproj` and Node are not needed.
   - Final stage: `mcr.microsoft.com/dotnet/aspnet:10.0`. Copy `/app`, `USER $APP_UID`, `ENTRYPOINT ["dotnet", "BrightPath.Api.dll"]`. The image listens on 8080 by default.
   - `LABEL org.opencontainers.image.source=https://github.com/duytech/TutoringSchedulingSDD`, so the GHCR package links to the repo.

2. **Web image** (`web/Dockerfile`, build context = `web/`, and `web/nginx.conf`)
   - Stage `build`: `node:22-alpine`. Copy `package.json` and `package-lock.json`, `npm ci`, copy the rest, `npm run build`.
   - Final stage: `nginx:alpine`. Copy `dist` to `/usr/share/nginx/html` and `nginx.conf` to `/etc/nginx/conf.d/default.conf`. Same `LABEL`.
   - `nginx.conf`: `listen 8080`, `location /api/ { proxy_pass http://api:8080; }`, `location / { try_files $uri /index.html; }`. One comment: it does in the container what the Vite proxy does in dev.

3. **Build context filters**
   - `.dockerignore`: `**/bin`, `**/obj`, `web`, `tests`, `specs`, `.git`, `.github`, `.assignment`, `.playwright-cli`.
   - `web/.dockerignore`: `node_modules`, `dist`, `obj`, `.playwright-cli`.

4. **Compose** (`compose.yaml`)
   - `db`: `postgres:17`, `POSTGRES_PASSWORD: postgres`, healthcheck `pg_isready -U postgres`. No ports and no volume.
   - `api`: `image: ghcr.io/duytech/tutoringschedulingsdd-api:latest`, `build` with `context: .` and `dockerfile: src/BrightPath.Api/Dockerfile`, the connection string from `requirements.md`, `depends_on: db: condition: service_healthy`.
   - `web`: `image: ghcr.io/duytech/tutoringschedulingsdd-web:latest`, `build: ./web`, `ports: ["8080:8080"]`, `depends_on: [api]`.

5. **Smoke test** (`scripts/smoke-test.sh`)
   - `set -euo pipefail`, plus `BASE=${1:-http://localhost:8080}`.
   - A small `check <path> <expected-text>` function: `curl -fsS` and `grep -qF`, printing `ok <path>` or a failure that names the path.
   - The 4 checks in `requirements.md`. Check 1 uses `curl --retry 45 --retry-delay 2 --retry-all-errors`. Check 4 compares `curl -o /dev/null -w '%{http_code}'` with `404`.
   - Mark it executable in git (`git update-index --chmod=+x`).

6. **CI job** (`.github/workflows/ci.yml`): a `container` job next to `api` and `web`.
   - Steps: checkout, `docker compose up -d --build`, `bash scripts/smoke-test.sh`.
   - On failure: `docker compose logs`. Always: `docker compose down`.

7. **CD workflow** (`.github/workflows/cd.yml`)
   - `on: workflow_run` (workflows: `[CI]`, types: `[completed]`, branches: `[main]`) and `workflow_dispatch`.
   - Job `publish`: `if: github.event_name == 'workflow_dispatch' || github.event.workflow_run.conclusion == 'success'`, and `permissions: contents: read, packages: write`.
   - Steps:
     - Check out `${{ github.event.workflow_run.head_sha || github.sha }}`.
     - `docker/login-action@v4` to `ghcr.io` with `GITHUB_TOKEN`.
     - `docker compose build`.
     - For `api` and `web`: tag `:latest` as `:<sha>`, then `docker push --all-tags`.

8. **Docs**
   - `README.md`: a "Run with Docker" section after the local run instructions, covering:
     - `docker compose up --build` from source, or `docker compose pull` then `docker compose up` for the published images;
     - `http://localhost:8080/rooms`;
     - `docker compose down` to remove it all;
     - no volume, so every start is a fresh seed.

     One line saying CD pushes both images for every green commit on `main`.
   - `specs/tech-stack.md`: "Local infra" becomes local Postgres for dev and tests, with Docker Compose as an optional way to run the app. The Docker bullet in "Not used" narrows to Docker for dev and Testcontainers.
   - `DECISIONS.md` §4 "Where it stopped": one bullet. After the stretch phases come CI on every push and pull request, and container delivery, so a reviewer can run the app with Docker alone.

9. **Validate** (see `validation.md`), then **commit**, push and open the pull request:
   `ci: deliver the API and web as container images`.
