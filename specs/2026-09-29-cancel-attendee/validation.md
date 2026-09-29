# Phase 13: Cancel attendee — Validation

The phase can be merged when every item below is true.

## Automated

- [ ] `dotnet build` succeeds with zero warnings.
- [ ] `dotnet test` passes: the existing tests, `CancelCheckTests` and `CancelAttendeeTests`.
- [ ] The edges in `plan.md` step 4 pass on both sides.
- [ ] The race test fails when the `FOR UPDATE` is removed (checked by hand, not committed), and passes again once it is back.
- [ ] Phase 12's schedule tests still pass: nothing on 03-06 or 03-07 was touched.

## Endpoint (curl, on a freshly seeded dev DB)

Attendee and session ids come from `GET /api/schedule?date=…`.

| # | Request | Expected |
|---|---|---|
| 1–13 | Rows 1–13 of the table in `requirements.md`, in order | As listed there |
| 14 | `GET /api/schedule` after row 1 | L020's session is `cancelled`, still listed under R3 and T3, with `changedAfterCutoff` true |
| 15 | Scalar UI / `/openapi/v1.json` | The cancel endpoint is listed, with its responses |

## Docs and commit

- [ ] `DECISIONS.md` §3: the cancel API row (session view, 404, the two 409 codes), the session change on the last cancel (and why the seed has none), and the rule rows for "already started" and the row lock.
- [ ] `specs/roadmap.md` phase 13 matches.
- [ ] Exactly one commit, Conventional Commits style, and the message says what and why.
- [ ] The dev DB is reseeded to the export afterwards.
