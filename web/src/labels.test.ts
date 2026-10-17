import { describe, expect, it } from 'vitest'
import type { TutorChange } from './tutorChanges'
import { cancelOutcomeLine, changeLine, cutoffLine, movedLabel, ruleLabel, shortDate } from './labels'
import { anAttendee, aSession } from './testData'

describe('movedLabel', () => {
  it('shows only the time and room when the session moved within the day', () => {
    const label = movedLabel('2026-03-07', { id: 'x', startsAt: '2026-03-07T16:00:00+07:00', roomId: 'R4' })

    expect(label).toEqual({ text: 'moved → 16:00 R4', otherDate: null })
  })

  it('names the day, and links to it, when the session moved to another day', () => {
    const label = movedLabel('2026-03-07', { id: 'x', startsAt: '2026-03-11T16:00:00+07:00', roomId: 'R4' })

    expect(label).toEqual({ text: 'moved → Wed 11 Mar 16:00 R4', otherDate: '2026-03-11' })
  })
})

describe('shortDate', () => {
  it('writes a date as weekday, day and month', () => {
    expect(shortDate('2026-03-06')).toBe('Fri 6 Mar')
  })
})

describe('changeLine', () => {
  const change: TutorChange = {
    sessionId: 's',
    sessionStartsAt: '2026-03-05T16:00:00+07:00',
    roomId: 'R3',
    kind: 'cancelled',
    attendeeId: 'a',
    studentName: 'Do Van Kien',
    changedAt: '2026-03-05T14:40:00+07:00',
    changedBy: 'tutor',
    note: 'tutor sick',
  }

  it('names the student, who changed it, when and why', () => {
    expect(changeLine(change)).toBe('16:00 R3 · Do Van Kien cancelled by tutor at 14:40 (tutor sick)')
  })

  it('says "session" for a change to the whole session, and leaves out what is missing', () => {
    const line = changeLine({ ...change, attendeeId: null, studentName: null, changedBy: null, note: null })

    expect(line).toBe('16:00 R3 · session cancelled at 14:40')
  })

  it('gives the day of a change made the evening before', () => {
    const line = changeLine({ ...change, kind: 'moved', changedAt: '2026-03-04T17:30:00+07:00', note: null })

    expect(line).toBe('16:00 R3 · Do Van Kien moved by tutor at Wed 4 Mar 17:30')
  })
})

describe('cutoffLine', () => {
  it('says since when the day is final, or until when it is not', () => {
    expect(cutoffLine({ cutoff: '2026-03-05T16:00:00+07:00', final: true })).toBe('Final since Thu 5 Mar 16:00')
    expect(cutoffLine({ cutoff: '2026-03-06T16:00:00+07:00', final: false })).toBe('Not final until Fri 6 Mar 16:00')
  })
})

describe('ruleLabel', () => {
  it('names a known rule in plain words', () => {
    expect(ruleLabel('student-overlap')).toBe('Student overlap')
  })

  it('shows an unknown rule code as it is', () => {
    expect(ruleLabel('new-rule')).toBe('new-rule')
  })
})

describe('ruleLabel for the write-only codes', () => {
  it.each([
    ['in-the-past', 'In the past'],
    ['already-started', 'Already started'],
    ['already-cancelled', 'Already cancelled'],
  ])('%s → %s', (code, label) => {
    expect(ruleLabel(code)).toBe(label)
  })
})

describe('cancelOutcomeLine', () => {
  const cancelled = (afterCutoff: boolean) => ({
    kind: 'cancelled' as const,
    attendeeId: 'attendee-1',
    changedAt: '2026-03-06T10:00:00+07:00',
    changedBy: 'family',
    afterCutoff,
    note: null,
  })

  it('says chargeable, late and the session cancelled when the last student goes late (L020)', () => {
    const session = aSession({
      cancelledAt: '2026-03-06T10:00:00+07:00',
      attendees: [anAttendee({ status: 'cancelled', chargeable: true })],
      changes: [cancelled(true), { ...cancelled(true), attendeeId: null }],
    })

    expect(cancelOutcomeLine(session, 'attendee-1')).toBe(
      'Bui An Nhien cancelled · chargeable · after the tutor was told · the session is cancelled',
    )
  })

  it('says only the name when nothing else is true, and the session stays while another student is booked', () => {
    const session = aSession({
      attendees: [
        anAttendee({ status: 'cancelled' }),
        anAttendee({ id: 'attendee-2', studentName: 'Do Van Kien' }),
      ],
      changes: [cancelled(false)],
    })

    expect(cancelOutcomeLine(session, 'attendee-1')).toBe('Bui An Nhien cancelled')
  })
})
