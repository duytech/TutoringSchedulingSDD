import { describe, expect, it } from 'vitest'
import type { BookingChange, Session } from './api'
import { changesAfterCutoff } from './tutorChanges'

function change(changedAt: string, afterCutoff: boolean, attendeeId: string | null = null): BookingChange {
  return { kind: 'cancelled', attendeeId, changedAt, changedBy: 'family', afterCutoff, note: null }
}

function session(id: string, startsAt: string, roomId: string, changes: BookingChange[]): Session {
  return {
    id,
    tutorId: 'T1',
    tutorName: 'Ngoc Anh',
    roomId,
    startsAt,
    endsAt: startsAt.replace(':00:00', ':59:00'),
    cancelledAt: null,
    movedToSessionId: null,
    movedTo: null,
    legacyViolation: false,
    attendees: [
      {
        id: 'a1',
        studentId: 's1',
        studentName: 'Do Van Kien',
        lessonId: 'L017',
        status: 'cancelled',
        cancelledAt: null,
        cancelledBy: null,
        chargeable: false,
        legacyViolation: false,
        note: null,
      },
    ],
    changes,
  }
}

describe('changesAfterCutoff', () => {
  it('lists only the changes after the cut-off, each with its session and student', () => {
    const changes = changesAfterCutoff([
      session('s-early', '2026-03-06T09:00:00+07:00', 'R3', [
        change('2026-03-05T15:00:00+07:00', false, 'a1'),
        change('2026-03-05T17:00:00+07:00', true, 'a1'),
      ]),
    ])

    expect(changes).toEqual([
      {
        sessionId: 's-early',
        sessionStartsAt: '2026-03-06T09:00:00+07:00',
        roomId: 'R3',
        kind: 'cancelled',
        attendeeId: 'a1',
        studentName: 'Do Van Kien',
        changedAt: '2026-03-05T17:00:00+07:00',
        changedBy: 'family',
        note: null,
      },
    ])
  })

  it('is oldest first across sessions, with the student before the session at the same time', () => {
    const at = '2026-03-06T09:00:00+07:00'
    const changes = changesAfterCutoff([
      session('s-early', '2026-03-06T11:00:00+07:00', 'R1', [change(at, true, 'a1'), change(at, true)]),
      session('s-late', '2026-03-06T14:00:00+07:00', 'R2', [change('2026-03-06T08:30:00+07:00', true)]),
    ])

    expect(changes.map((late) => [late.sessionId, late.attendeeId])).toEqual([
      ['s-late', null],
      ['s-early', 'a1'],
      ['s-early', null],
    ])
    expect(changes[2].studentName).toBeNull()
  })

  it('is empty when nothing changed after the cut-off', () => {
    expect(changesAfterCutoff([session('s', '2026-03-06T09:00:00+07:00', 'R1', [])])).toEqual([])
  })
})
