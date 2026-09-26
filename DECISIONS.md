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
