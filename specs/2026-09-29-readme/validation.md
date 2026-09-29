# Phase 14: README — Validation

The phase can be merged when every item below is true.

## Followed as written

On a freshly dropped dev database, in bash:

- [ ] Every command in "Run", "Test" and "Try it" works exactly as written, in order.
- [ ] The outputs match "What I saw", apart from ids and anything that depends on `now`.
- [ ] `Clock__Now=2026-03-07T10:00:00+07:00 dotnet run …` makes `GET /api/schedule` return 2026-03-07.
- [ ] The Scalar and OpenAPI URLs open.
- [ ] `dotnet test` passes, and no `brightpath_test_%` database is left afterwards.

## Content

- [ ] The sections from `requirements.md` are there, in order.
- [ ] The pinned date (2026-03-06 10:00 +07:00) is stated near the top.
- [ ] Prerequisites name the .NET SDK, PostgreSQL 13+, `CREATE DATABASE` and `CREATE EXTENSION btree_gist`.
- [ ] The connection string can be changed without editing a committed file (user-secrets or env), and the README says the tests use the same setting.
- [ ] "What I saw" is real output, trimmed with `…` only. Each block has one sentence.
- [ ] No design reasoning is repeated from `DECISIONS.md`. It is linked instead.
- [ ] No password other than the documented default `postgres`/`postgres` appears.
- [ ] Every link resolves.
- [ ] It fits in about two screens.

## Docs and commit

- [ ] Exactly one commit, Conventional Commits style (`docs:`), and the message says what and why.
- [ ] The dev DB is reseeded to the export afterwards.
