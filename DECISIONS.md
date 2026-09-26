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
- **The cut-off and the 4-hour window are independent.** L005 was cancelled at 08:15 for a 14:00 lesson the same day. That is 5h45m before the start, so it is free for the family. But it is after the 16:00 cut-off the day before, so it is shown as a change.
