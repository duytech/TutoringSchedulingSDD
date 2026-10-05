# Phase 26: Container delivery — Validation

## Local (Docker Desktop running)

- [ ] `docker compose up -d --build` builds both images and starts `db`, `api` and `web`.
- [ ] `bash scripts/smoke-test.sh` prints `ok` for all 4 checks.
- [ ] The smoke test fails when the stack is down (`docker compose stop web`): it exits non-zero and names the path.
- [ ] `docker compose down` removes everything. A second `up` starts from a fresh seed.

## Browser (Playwright CLI, against `http://localhost:8080`)

| # | Check | Expected |
|---|---|---|
| 1 | `/rooms?date=2026-03-06` | The room grid with the seeded lessons. "now" is at 10:00 |
| 2 | Click a tutor link | `/tutors/…` opens and shows that tutor's day |
| 3 | Reload on `/tutors/T1` | The same page, not an nginx 404 |
| 4 | `/violations` | "4 rule breaks" |

## Unchanged

- [ ] `git diff --stat` touches no file under `src/**/*.cs`, `web/src` or `tests/`.

## GitHub

- [ ] On the pull request, CI has three green jobs: `api`, `web` and `container`.
- [ ] After merge, the CD run on `main` succeeds, and both packages show the commit SHA and `latest` tags.
- [ ] After the owner sets both packages to public, `docker compose pull` followed by `docker compose up` runs the published images, and the smoke test passes.

## Commit

- [ ] Exactly one commit.
