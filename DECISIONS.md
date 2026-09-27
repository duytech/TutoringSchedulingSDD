# Decisions

Bright Path Learning Centre: scheduling and conflict detection. This file follows the four phases of the brief.

## 1. Reading the situation

### Questions for the owner

These are the questions I would ask first. Each has the default I build with while there is no answer, and what changes if the answer is different. The defaults are config values or small code branches, so a different answer is cheap to apply.

| # | Question | My default | If the answer is different |
|---|---|---|---|
| Q1 | An exam pair is one tutor with two students. Does it count as **1 or 2 bookings** toward the 6-per-day tutor limit? | **1.** The limit counts sessions (teaching slots), not students. | If 2: the load check adds up active attendees instead of sessions. Some seeded days may then exceed the limit and get flagged. |
| Q2 | When the **tutor or the centre** cancels inside the 4-hour window (L017, "tutor sick", 1h20m before), is the family charged, and is the tutor paid? | Only a **family** cancellation inside 4 hours is chargeable. Tutor or centre cancellations are never charged to the family. | We already record `cancelledBy`. If the family should be charged anyway, the chargeable rule stops looking at `cancelledBy`. If the tutor loses pay when they cancel, we add a separate tutor-pay flag. Billing itself stays out of scope. |
| Q3 | What are the **exact opening hours**? The brief says "mid-morning to mid-evening", but the data has lessons from 09:00 to 21:30. | A config window that fits what the centre actually ran: first start 09:00, last end 21:30. | Only the config changes. Seeded rows outside the new window get flagged in the violation report. New bookings outside it are refused. |
| Q4 | The brief says **six rooms**, but the data only uses R1 to R3. Are all six rooms the same and bookable for any lesson? | **Yes.** R1 to R6 are seeded as identical rooms. | If some rooms are special (e.g. only one fits a pair, or one is kept for exams), a room gets a capacity or type column and the create check tests it. |
| Q5 | Mai breaks the 6-per-day rule "when she is desperate", and L032 was moved onto a closed Monday. Should the receptionist ever be allowed to **override a rule**? | **No.** Every rule is a hard refusal (409). The owner said the load rule must be enforced. | If yes: create takes an `overrideReason`, which is stored as a change record. It would apply only to policy rules (load, Monday, hours). It would **never** apply to overlaps, since a student in two places is the one thing the owner said must never happen. |
| Q6 | What does "**after the tutor was told**" mean? Is it the 16:00 cut-off the day before, or the moment Mai actually sends the message? | The **cut-off** is the moment. Any change to a lesson after 16:00 the day before (including same-day changes) is marked `after_cutoff`. | If it is the real send time, we need a "day published to tutor" record per tutor per day, and the flag compares against that time instead of a fixed 16:00. |
| Q7 | What are the **limits of an exam pair**? Always at most 2 students? Any two students, or only the same subject or level? Only in exam season? | Up to **2 attendees** per session, any two students, any time of year. A second student can be added to an existing session if they are free. | If 3 are allowed, the limit becomes a config value. If pairs are only for exam season or must share a subject, the create check adds a date range or a subject match. |

### Where the brief and the data disagree

The general rule: **the export is history, and history is a fact.** Rows that break a rule are loaded as they are and flagged in a violation report. They are not fixed or dropped. The rules apply to every **new** write.

| What conflicts | Evidence | Reading I chose |
|---|---|---|
| "Lessons are one-to-one" and "a room holds one lesson at a time" vs. exam pairs, which Mai runs "most weeks" | L009 + L010: T1, R1, 2026-03-04 11:00, both "exam pair - half price" | A **session** is one tutor, one room, one time slot, with **1 or 2 attendees**. An exam pair is one session with two attendees, so the room rule still holds. The seed loader groups L009 + L010 into one session. |
| A student in two places at once, the exact thing the owner says must never happen | L007 + L008: Le Minh Chau, 2026-03-04 09:00, in R3 with T3 **and** in R2 with T2. The note says "added by phone; family confirmed" | Loaded and flagged. It is proof that the problem is real and is still happening. A new booking like this is refused. |
| Closed on Monday vs. a Monday lesson | L032: 2026-03-09 (Monday), "moved from Sunday at the family's request" | Loaded and flagged. A new Monday booking is refused. Whether Mai may ever override this is Q5. |
| No more than 6 bookings per tutor per day | T1 on 2026-03-06 has 7: L018, L021, L022, L024, L025, L026, L027 | Loaded and flagged. A 7th booking is refused. The pair on 03-04 counts as 1 (Q1). |
| A tutor can only be in one room at a time | L033 + L034: T1, 2026-03-10 09:00, in R1 **and** R2 | Loaded and flagged. A new booking like this is refused. |
| Opening hours are "mid-morning to mid-evening" vs. what the centre ran | Lessons start at 09:00 (L001, L002, …) and L027 ends at 21:30 | The window is config, set to 09:00–21:30 so it matches what was actually run (Q3). No seeded row is flagged for hours under this default. |
| The late-cancellation rule is written about the family, but the tutor cancelled late | L017: cancelled 14:40 for a 16:00 lesson (1h20m before), note "tutor sick" | A late cancellation is chargeable only when the **family** cancels. We record who cancelled (Q2). |
| "Tomorrow's schedule is final at 16:00 today", but the day before Tuesday is Monday, when the centre is closed | Tue 2026-03-03 and Tue 2026-03-10 lessons | The cut-off is 16:00 on the calendar day before, even when that day is a Monday. A change to a Tuesday lesson made on Monday evening counts as a change after the cut-off. |
| The export is described as "one week" | Rows run from Tue 2026-03-03 to Tue 2026-03-10: eight days, two Tuesdays. The export was taken on 2026-03-10 | All eight days are loaded. The 03-10 rows were still plans when the export was taken. |
| 12 tutors, ~200 families and 6 rooms vs. the export | The export has 3 tutors (T1–T3), 6 students and rooms R1–R3 | The export is a slice of the centre. Tutors and students come from the data. Rooms R1–R6 are seeded as reference data (Q4). Nothing in the code depends on these counts. |

### Assumptions I had to invent

- **Today is pinned to Friday 2026-03-06, 10:00 (+07:00).** It is the busiest day in the export and shows the tutor-overload case. At 10:00 some of the day's lessons are already over and some have not started, so the 4-hour late-cancellation window can be shown on real rows. The clock is injected, and nothing reads the real time.
- **All times are local to Da Nang (Asia/Ho_Chi_Minh, +07:00).** The CSV lesson times have no offset. The `cancelled_at` values do, and they are all +07:00.
- **A student is identified by their name.** The export has no student ID and no family record. The same name means the same child.
- **The rule numbers come from the brief as rendered:** 6 bookings per tutor per day, 4 hours, 16:00, Tuesday to Sunday, 60 or 90 minutes. The brief warns that copied text may not match what is shown, so these values live in config. A misread is then a config change, not a code change.
- **Cancelled frees the slot, and no-show does not.** This follows the brief. A late family cancellation is charged but **still** frees the room and the slot. L015 (no-show) keeps its slot.
- **Who cancelled is read from the note.** The export has no such column, but the notes say it: "family cancelled" (L005) is the family, "tutor sick" (L017) is the tutor. A note with no such word leaves it unknown.
- **When two seeded rows overlap at the same start time, the higher lesson ID is the "later" one.** So L008 is flagged rather than L007, and L034 rather than L033.
- **The cut-off and the 4-hour window are independent.** L005 was cancelled at 08:15 for a 14:00 lesson the same day. That is 5h45m before the start, so it is free for the family. But it is after the 16:00 cut-off the day before, so it is shown as a change.

## 2. Choosing what to build

### Features this tool could need

1. **Conflict-safe booking.** Creating or cancelling a lesson is refused if it breaks a centre rule: a student, room or tutor in two places at once, more than 6 a day for a tutor, a Monday, or outside opening hours.
2. **Today board.** One screen with a room × time grid for today, so the owner can open the laptop and see the day without scrolling.
3. **Tutor day sheet.** Each tutor's day as one authoritative page, with changes after the cut-off marked, so there is never a "which message is real?".
4. **Tutor notifications.** The day and any later changes are sent to tutors over WhatsApp or Zalo, instead of Mai typing them.
5. **Family self-cancel.** A family cancels through a link at night, and the schedule updates straight away instead of the next morning.
6. **Slot backfill.** When a slot frees up, the next family who wants it is told.
7. **Late-cancel billing.** Charge the family for a late cancellation and track what the tutor is paid.

### The pick: 1, conflict-safe booking

- **It is the only pain the owner called non-negotiable.** "If the system allows it, the system is broken." The export shows it is still happening: a student in two rooms (L007/L008), a tutor in two rooms (L033/L034), and a tutor with 7 lessons in one day.
- **Every other feature sits on top of it.** A board, a day sheet or a notification only shows what is in the schedule. If the schedule can hold a double-booking, those features just spread the mistake faster. The schedule has to be right first.
- **Mai leaves in eight weeks, and the rules leave with her.** Today the rules exist only in her head, and she breaks them "when desperate". Whoever covers for her needs a tool that holds the rules for them.
- **It fits the time box and can be proven.** The overlap rules are enforced by the database. Each rule has a test that shows the booking being refused.

What the feature includes, and why:

- **Create a session.** This is where a conflict gets in.
- **Cancel an attendee.** A cancellation frees the slot, so the conflict rules depend on it. It is also where "changed after the tutor was told" happens, and the data model has to survive that.
- **Read one day as JSON.** This is the minimum needed to see that the rules hold. It is not the Today board (feature 2): there is no grid and no UI.

### Why the others wait

- **2 and 3 (board, day sheet)** are the next cheapest win. They are reads over the same model, and they are only worth building once the data under them can be trusted.
- **4 (notifications)** needs a WhatsApp/Zalo business account, templates and costs. That is a decision for the owner, not a 2.5-hour build.
- **5 (self-cancel)** needs families to have links or logins. That is a new group of users with their own security questions.
- **6 (backfill)** needs a waiting list, and the centre does not keep one today.
- **7 (billing)** is about money, not about the daily pain anyone described. We only flag a late cancellation as chargeable.

### What I leave broken by choosing it

- **Tutors still get their day from Mai's messages.** The "which message is real?" problem is not solved. Changes are recorded and marked as after the cut-off, but nothing pushes them to the tutor.
- **A tutor can still drive in for a cancelled lesson** if nobody tells them. The system knows about the cancellation. The tutor does not.
- **The owner cannot "see today" on a screen yet.** There is only a JSON endpoint, unless the stretch phase for the board gets done.
- **Families still cancel on WhatsApp at night**, and Mai still enters it the next morning.
- **Freed slots are not offered to anyone.**
- **Late cancellations are flagged, not billed**, and tutor pay is not tracked.
- **No move endpoint in the core.** For now a move is a cancel plus a new booking. A linked move is a stretch phase.
- **No login.** Anyone at the laptop can book or cancel.

## 3. Design

### Data model

PostgreSQL. Only the tables this feature needs.

| Table | Key columns | Notes |
|---|---|---|
| `tutors` | `id` (`T1`…), `name`, `subject` | From `tutors.csv`. |
| `rooms` | `id` (`R1`…`R6`) | Reference data (Q4). |
| `students` | `id` (uuid), `name` (unique) | Built from the names in the export (a name is the identity). |
| `sessions` | `id` (uuid), `tutor_id`, `room_id`, `starts_at`, `ends_at`, `slot`, `cancelled_at`, `moved_to_session_id`, `legacy_violation` | One tutor, one room, one time slot. `slot` is the range `[starts_at, ends_at)`, generated from the two columns. `CHECK` that the length is 60 or 90 minutes. A session is **active** while `cancelled_at` is null. |
| `attendees` | `id` (uuid), `session_id`, `student_id`, `slot`, `status`, `cancelled_at`, `cancelled_by`, `chargeable`, `legacy_violation`, `source_lesson_id`, `note` | One student in one session. A session has 1 or 2. `status` is `booked`, `cancelled` or `no_show`. `cancelled_by` is `family`, `tutor` or `centre`. `slot` is copied from the session so the database can check a student's overlaps. `source_lesson_id` keeps the CSV lesson ID (`L001`…), because one CSV row is one attendee and the violation report has to name it. |
| `booking_changes` | `id` (uuid), `session_id`, `attendee_id`, `kind`, `changed_at`, `changed_by`, `after_cutoff`, `note` | Append-only log. `kind` is `created`, `cancelled` or `moved`. |

- **Keys:** `tutors` and `rooms` keep the codes people use (`T1`, `R1`). The other tables use uuids created in code, so a session, its attendees and its change record can be built together before one insert.
- **Enum-like columns** (`status`, `cancelled_by`, `kind`) are `text` with a `CHECK`, not Postgres enums, so adding a value later is a small migration.
- **An exam pair** is one session with two attendees (L009 + L010).
- **Copying `slot` onto attendees is safe** because a session's time never changes after it is created. There is no update endpoint (see the rejected endpoint below), so the copy cannot go stale.
- **No-show** keeps the attendee as `no_show`. The session stays active and the slot stays taken.
- **When the last attendee of a session is cancelled**, the session is cancelled too, and that frees the room and the tutor.

### A booking cancelled or moved after the tutor was told

- **Nothing is deleted or overwritten.** A cancel only sets the status fields, and a time never changes in place.
- **Every change writes a `booking_changes` row**, including a new booking. `after_cutoff` is true when the change happens after 16:00 on the day before the lesson. So "added after the tutor was told" and "cancelled after the tutor was told" both show up.
- **Cancel:** the attendee becomes `cancelled`, with who cancelled and when. `chargeable` is true when the **family** cancels less than 4 hours before the start (Q2).
- **Move** (stretch phase): in one transaction, cancel the old session, create the new one, and point `moved_to_session_id` from the old to the new. The tutor can see both "your 14:00 is gone" and "it is now at 16:00".
- **Seed:** each cancelled row gets a `cancelled` change at its `cancelled_at`. L005 and L017 both come out as after the cut-off. Booked rows get no `created` change, because the export does not say when they were made.

### Where each rule is enforced

| Rule | Where | How |
|---|---|---|
| A room holds one session at a time | **Database** | `EXCLUDE USING gist (room_id WITH =, slot WITH &&)` on active sessions |
| A tutor is in one room at a time | **Database** | Same, on `tutor_id` |
| A student is in one place at a time | **Database** | Same, on `attendees.student_id`, for attendees that are not cancelled |
| 60 or 90 minutes, status values | **Database** | `CHECK` |
| At most 2 attendees per session | Code | Checked in the same transaction as the insert |
| At most 6 sessions per tutor per day | Code | Counted in a transaction that holds a lock for that tutor and day, so two receptionists cannot both take the 6th slot |
| Closed on Monday, opening hours | Code | Config values, so the owner's answers (Q3, Q5) do not need a migration |
| Late cancellation is chargeable | Code | Worked out at cancel time and stored on the attendee |
| A change after the cut-off is flagged | Code | Worked out when the change is written and stored in `after_cutoff` |

- **Why this split:** a rule that must hold even when two people click at the same moment goes in the database. An overlap check done in code can always lose a race. A rule the owner may still change (6 a day, 4 hours, 16:00, opening hours) goes in code and config.
- **Seeded history vs. the constraints:** the export has two overlapping pairs (L007/L008 and L033/L034). Only the **later** row of each pair is marked `legacy_violation` and left out of the constraint. The earlier row is still covered, so the database still guards that slot against new bookings.
- **`attendees.slot` is filled by a `BEFORE INSERT` trigger** that copies it from the session. No insert path (the seed loader, the create endpoint) can forget it or set it wrong. Neither `slot` column is mapped in EF: they exist only for the constraints.
- **Cancelling a session must cancel all its attendees.** The student constraint looks only at the attendee's own `status`, so an attendee left `booked` on a cancelled session would still block that student. Cancel and move keep this, the same way the last attendee cancelled cancels the session.
- **Before every write, the code also checks all the rules**, so a 409 can list every conflict at once in plain words. The database is the backstop. If a race gets past the code checks, the constraint error is turned into the same 409.
- **The code rules are written once**, in one rule set. The violation report runs it over the loaded schedule, and create runs it over the new session and that day's sessions. The report and the 409 use the same rule codes, so they cannot disagree about what a rule means.

### API

| Method and path | Does | Returns |
|---|---|---|
| `GET /api/schedule?date=2026-03-06` | One day's sessions, grouped by room and by tutor, with attendees, status and change flags. Defaults to the pinned today. | 200 |
| `POST /api/sessions` | Body: `tutorId`, `roomId`, `startsAt` (local time with offset), `durationMin`, `studentIds` (1 or 2). | 201 with `Location`. 400 for bad input. **409** `ProblemDetails` with a `conflicts` list, e.g. `student-overlap: Le Minh Chau is in R3 with T3 at 09:00` |
| `POST /api/sessions/{id}/attendees/{attendeeId}/cancel` | Body: `cancelledBy` (`family`, `tutor` or `centre`). | 200 with `chargeable` and `afterCutoff`. 409 if already cancelled. |
| `GET /api/reports/violations?from=&to=` | Every rule the loaded schedule breaks. `from` and `to` are optional local dates. One item per problem (rule code, date, sessions, lesson IDs, a plain message), so an overlapping pair is one item. Late cancellations and changes after the cut-off are allowed, so they are not listed. | 200. 400 if `from` is after `to` |

Stretch, after the cut line: `POST /api/sessions/{id}/move` and `GET /api/tutors/{id}/day?date=`. The model already allows adding a second student to an existing session, but there is no endpoint for it yet.

### Endpoint I rejected: `PUT /api/sessions/{id}`

A general "edit this session" endpoint that changes the time, room or tutor in place.

- **It is exactly the "quietly overwriting" the brief forbids.** After the cut-off, the tutor was told one thing, and a `PUT` replaces it with no trace of what they were told.
- **One verb would hide several different events.** A new time, a new room and a new tutor affect different people, and each needs its own conflict checks and its own change record.
- **It would break the copied `slot`** on attendees, which is only safe because times never change in place.

Instead, each change has its own named action (cancel, and later move), and each one leaves a record.
