# Phase 15: DECISIONS §4: reflection — Requirements

Roadmap: Stage C, phase 15 (est. 10 min). One atomic commit. This is the last phase above the ✂️ cut line.

## Goal

Close `DECISIONS.md` honestly, as the brief's step 4 asks: "what you would build next with another week, what you know is weak, where your AI assistant helped, and one suggestion you threw away, and why you were right to."

The brief marks "Communication and reflection" on whether the reflection "is honest about what is weak", and it says "we assume you used one [an AI assistant] and will ask about it, so please do not pretend otherwise". So every line in §4 must be something the author can defend at the interview. Nothing about the AI or the time spent is guessed.

## In scope

- A new `## 4. Reflection` at the end of `DECISIONS.md`.
- One line in §2 "What I leave broken", which still says the board depends on "the stretch phase … gets done".
- One line in `specs/roadmap.md` saying the stretch phases were not built.

## Out of scope

- Any code change. A weak spot is named, not fixed, in this phase.
- The stretch phases (16–19).
- Rewriting §1–§3.

## Decisions (confirmed with the owner of this repo)

| Topic | Decision |
|---|---|
| Stretch phases | **Stopped at the ✂️ line.** The roadmap already used 152 of the 150 minutes. §4 says plainly that there is no UI, that the Today view exists only as JSON, and puts the UI first in next week's list. |
| AI suggestion thrown away | **Cleaning the export before loading it** (dropping or fixing L008, L034 and L032). It was kept as history and flagged instead. |
| Where the AI helped | Reading the brief and the data, the spec for each phase, code and tests, and catching mistakes / proving things. |
| Time spent | **Stated honestly**: the real total against the 2.5-hour box, and what took longer than estimated. The author gives the number when the phase is built. It is not worked out from commit times. |

## Shape

A few bullets per subsection, as the brief asks ("A few bullets per written section is plenty"). The same voice as §1–§3: first person, short sentences, lesson IDs as evidence.

### Where it stopped

- Phases 1–15 are done. The stretch phases (React Today view, the changes badge, move, the tutor day) are not.
- The Today view exists only as `GET /api/schedule` JSON, as the roadmap said it would if phase 16 was skipped.
- **Time:** the real total against the 2.5-hour box, in one sentence, and the one or two parts that took longer than their estimate. Filled in by the author.

### Next week

In order, with one line each on why it comes at that point:

1. **Tutor day sheet and Today board** (features 2 and 3, stretch phases 16, 17, 19). They are reads over the model that now holds, and they fix the pain the pick left broken: "which message is real?" and the owner not being able to see today.
2. **Move** (stretch phase 18): one transaction, `moved_to_session_id`, so a tutor sees "your 14:00 is gone" and "it is now at 16:00" together.
3. **The owner's answers to Q1–Q7.** Most of them change only config (the load count, the hours, the pair limit). Q6 would need a "day sent to the tutor" record.
4. **Login, so `changed_by` names a person** and not only family/tutor/centre. It is also the first step before families can cancel for themselves (feature 5).
5. **Decide on notifications with the owner** (feature 4): the account, the templates and the cost come before any code.

### Known weak spots

Each one is true of the code as it is, and each says what it would take to fix. Candidates, checked against the code when the phase is built:

- **The flagged rows' slots are guarded by code only.** L034's session and L008's attendee are left out of the exclusion constraints, so for those two slots the database no longer refuses an overlap. The code check still does, because the flagged rows are committed and always read. Fix: once the owner decides what really happened on those days, cancel or move the flagged rows and drop the flags.
- **The rules that count are code only.** The 6-a-day load and the 2-per-session limit are checked in code, under an advisory lock for the load. Anything that writes to the database without going through the API skips them. Fix: a trigger, if another writer ever appears.
- **No login.** Anyone at the laptop can book or cancel, and `cancelledBy` is whatever the request says.
- **The export can be loaded only once**, into an empty database. A second export of a later week has no import path.
- **The API tests share one database**, and stay apart only because each test books on a date of its own. A new test that reuses a date can break another test. The convention is written in the test classes, not enforced.
- **The pinned clock gives every change in one run the same time.** The view orders ties (the student before the session), but two changes of the same kind at the same time come back in no fixed order.
- **Startup logs every SQL statement** in Development, and the first start logs a `fail` line that is not an error (the README says so).

### Where the AI helped

Concrete examples from this repo, one line each:

- **Reading:** listing where the brief and the export disagree (the exam pair, the Monday lesson L032, T1's 7 sessions, L034) and turning them into questions for the owner.
- **Specs:** one requirements/plan/validation per phase, with the open decisions asked before any code.
- **Code and tests:** the endpoints, the rule set, the seed loader and the tests, written to the spec, then reviewed and run by me.
- **Catching mistakes:** the race tests hold a transaction open instead of firing parallel requests, which would pass without any guard. The health test was migrating and seeding my dev database on every `dotnet test`. Removing each guard by hand proved its test fails. The README was followed from a dropped database before it was committed.

The author reviews these lines and removes anything they do not recognise as true.

### An AI suggestion I threw away

**Clean the export before loading it:** drop or fix the rows that break the rules (L008, L034, L032), so the constraints can be switched on over clean data.

Why I was right to refuse it, in a few bullets:

- The export is what the centre actually ran that week (the export's own README says so). Dropping a row deletes a lesson a family came to, and was maybe charged for. Fixing it invents a time or a room nobody used.
- The broken rows are the evidence for the pick. The violation report shows the owner the four breaks in plain words. A clean import would hide exactly the problem the feature exists to stop.
- Flagging only the later row of each pair keeps the earlier row under the constraints, so the database still guards that slot against new bookings. The price is the weak spot above, and it is named.

The author reviews this text too. It has to be the argument they would make at the interview.

## Context

- The brief, step 4 and "How we will read your submission".
- `DECISIONS.md` §2 (features, what is left broken), §3 (design).
- `specs/roadmap.md` (the cut line, the note about phase 16).
- `specs/tech-stack.md` "Seed history vs. constraints".
