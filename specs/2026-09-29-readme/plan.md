# Phase 14: README — Plan

Tasks in order. The whole phase is one commit at the end.

1. **Check the facts the README states**
   - The port from `launchSettings.json` (`5238`).
   - The Scalar and OpenAPI URLs, by opening them on the running app.
   - That `Clock__Now=… dotnet run` moves today (`GET /api/schedule` returns that date).
   - That the tests read user-secrets and the environment variable (`BrightPathApiFactory.BaseConnectionString`).

2. **Fresh run, capturing the output**
   - Drop the dev database with `psql` (not on PATH on this machine, see the local Postgres setup).
   - `dotnet run --project src/BrightPath.Api`. Save the seed log line.
   - Run the "Try it" curl commands exactly as they will be written in the README, in bash. Save each response.
   - `dotnet test`. Save the summary line.

3. **Write `README.md`**
   - The sections from `requirements.md`, in order.
   - "What I saw": paste the saved output. Trim long JSON with `…`, but never change a value.
   - Every command is copied from what was actually run in step 2.

4. **Read it as the reviewer**
   - Follow the README top to bottom on a freshly dropped database: every command works as written, and the output matches "What I saw" (ids and `now`-based values aside).
   - Every link resolves (`DECISIONS.md`, `specs/roadmap.md`, a phase spec folder).
   - It fits in about two screens.

5. **Clean up**
   - Drop and reseed the dev database, so it is back to the export.

6. **Docs in the same commit**
   - `specs/roadmap.md` phase 14: unchanged, unless something in it turned out wrong.

7. **Commit**
   - `docs: README with prerequisites, run commands and what I saw`. The body says why: the reviewer runs this on their own Postgres, so the prerequisites and the connection-string override come first, and "What I saw" is the real output of one run so they can compare theirs against it.
