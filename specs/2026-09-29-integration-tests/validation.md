# Phase 12: Integration tests: conflicts — Validation

The phase can be merged when every item below is true.

## Automated

- [ ] `dotnet build` succeeds with zero warnings.
- [ ] `dotnet test` passes: the existing unit tests and tests 1–12 from `requirements.md`.
- [ ] `dotnet test` passes twice in a row, so nothing depends on a database left over from an earlier run.
- [ ] Each overlap test (1–3) gets exactly one conflict, with its own rule.

## The guards are really tested (done by hand, not committed)

| # | Change | Expected |
|---|---|---|
| 1 | Comment out the `pg_advisory_xact_lock` call in `SessionEndpoints` | Test 8 fails: the booking did not wait, or it got 201 |
| 2 | Remove the `23P01` catch | Test 7 fails with a 500 |
| 3 | Restore both | All tests pass |

## Databases

- [ ] After the runs, no `brightpath_test_%` database is left (`psql -l`).
- [ ] The dev database `brightpath` has the same number of sessions before and after `dotnet test`.
- [ ] Test 11 passes: the host is on a `brightpath_test_` database.

## Docs and commit

- [ ] `specs/roadmap.md` phase 12 names the two race tests, the health test move, and est. 17.
- [ ] `specs/tech-stack.md` Tests row says one database per run, each test on its own date, races forced with a held transaction.
- [ ] Exactly one commit, Conventional Commits style (`test:`), and the message says what and why.
