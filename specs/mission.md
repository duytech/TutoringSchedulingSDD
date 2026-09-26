# Mission

## Context

Bright Path Learning Centre (Da Nang) runs one-to-one tutoring with 12 tutors, ~200 families and 6 rooms. Today everything lives in one shared spreadsheet plus WhatsApp. Mai, the receptionist, is the only person who understands the sheet, and she goes on leave in **8 weeks**. The owner wants the **smallest internal tool that takes the daily pain away**.

Source brief: `.assignment/bright-path-learning-centre-assignment.md`. Seed data: `.assignment/lessons_export.csv`, `.assignment/tutors.csv`.

> The brief warns that it is set in a custom typeface and that copied text may not match what is shown. Before any rule is coded, check each number (6 bookings/day, 4-hour window, 16:00 cut-off, Tue–Sun) against the original rendering.

## What actually hurts (ranked)

1. **Double-booking.** A student was booked in two places at once, twice last term. The owner says: *"if the system allows it, the system is broken."* This is a hard requirement.
2. **No single source of truth for "today".** The owner wants to open the laptop and *see* today. Tutors get a message, then corrections, and do not know which one is real.
3. **Silent late changes.** Cancellations arrive at night. Tutors drive in for cancelled lessons. Changes after the cut-off overwrite what tutors were told instead of showing up as changes.

Mentioned but lower priority: billing for late cancellations, WhatsApp notifications, backfilling freed slots.

## The one feature: Conflict-safe schedule

The brief asks for exactly one feature. We build **one feature with a write side and a thin read side over the same model**:

- **Write (core):** create and cancel lessons. The system **refuses** any booking that breaks the centre's rules:
  - A student in two places at once.
  - A room holding two different lessons at once.
  - A tutor in two rooms at once.
  - More than 6 bookings for one tutor in a day.
  - A booking on Monday (closed) or outside opening hours.
  - A duration other than 60 or 90 minutes.
- **Read (thin):** a **Today view**, the schedule for the pinned date by room and by tutor. It is the proof that the write side holds, and it answers the owner's "see today".

If time runs short, the Today UI is cut first. The write side and its rules are not negotiable.

### What we deliberately leave broken

- No notifications (WhatsApp/SMS). Tutors still need to be told by a person.
- No billing or invoicing. Late cancellation is only *flagged* as chargeable.
- No finding or suggesting free slots, and no waitlist.
- No auth or roles. This is an internal tool on one laptop.
- No recurring or series bookings.

## Key interpretations (chosen readings)

| Tension in the brief | Our reading |
|---|---|
| "A room holds one lesson at a time" vs. exam pairs (2 students, 1 tutor, 1 room, same slot, "most weeks") | A **Session** is tutor + room + time slot and has **1–2 attendees**. Room and tutor exclusivity apply to Sessions; student exclusivity applies to Attendees. An exam pair is one Session with 2 attendees, so the room rule still holds. |
| "Max 6 bookings per tutor per day": does an exam pair count as 1 or 2? | Assumption: count **Sessions** (teaching slots). This goes on the list of questions for the owner. |
| Seed data breaks the rules (L007/L008 student double-booked, T1 has 7 lessons on 03-06, L032 on a Monday, L034 has a tutor in two rooms, lessons at 09:00 and 20:30 vs. "mid-morning to mid-evening") | **Load history as-is and flag the violations.** The past is fact. Rules apply to **new writes**. A report lists every violation found in the seed. |
| L017 is cancelled 1h20m before start, but the reason is "tutor sick" | Late-cancellation charging applies only when the **family** cancels. We record who cancelled. |
| 12 tutors / 6 rooms vs. 3 tutors / 3 rooms in the data | Rooms R1–R6 are reference data. Tutors come from `tutors.csv`. |
| "Cancelled" frees the slot, "no show" does not | Only `cancelled` attendees and sessions are ignored by the conflict checks. `no_show` still occupies the slot. |

## Pinned date

- **Today = 2026-03-06 (Friday)**, Asia/Ho_Chi_Minh (+07:00). It is the busiest day in the seed and shows the tutor-overload case.
- The clock is injected (`TimeProvider`) and configurable. Nothing reads the real clock.

## Changes after the cut-off

Tomorrow's schedule is final at 16:00 today. After that:

- Rows are never deleted or overwritten.
- A cancel changes the status, and a move cancels the old session and links it to the new one.
- Every change writes a `BookingChange` record with `after_cutoff = true/false`, so it is visible as a change.

## Success criteria

- With a local PostgreSQL running, `dotnet run` starts the API, migrates the DB and loads the seed.
- Every rule listed above has an integration test that proves the system refuses the booking.
- `GET` today returns the schedule for 2026-03-06. Seed violations are listed, not hidden.
- `DECISIONS.md` covers the 4 phases of the brief, and a reviewer who was not in the room can follow it.
- The git history is atomic, and each commit message says what and why.
- The time box is **2.5 hours** of build time.
