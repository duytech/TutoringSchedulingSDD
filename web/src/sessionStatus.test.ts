import { describe, expect, it } from 'vitest'
import type { BookingChange } from './api'
import { changedAfterCutoff, isCancelled, sessionState } from './sessionStatus'

const nineToTen = { startsAt: '2026-03-06T09:00:00+07:00', endsAt: '2026-03-06T10:00:00+07:00' }

function change(afterCutoff: boolean): BookingChange {
  return { kind: 'cancelled', attendeeId: null, changedAt: '2026-03-05T17:00:00+07:00', changedBy: 'family', afterCutoff, note: null }
}

describe('sessionState', () => {
  it('is upcoming just before the start', () => {
    expect(sessionState(nineToTen, '2026-03-06T08:59:59+07:00')).toBe('upcoming')
  })

  it('is in progress from the start itself', () => {
    expect(sessionState(nineToTen, '2026-03-06T09:00:00+07:00')).toBe('in-progress')
    expect(sessionState(nineToTen, '2026-03-06T09:59:59+07:00')).toBe('in-progress')
  })

  it('is past from the end itself', () => {
    expect(sessionState(nineToTen, '2026-03-06T10:00:00+07:00')).toBe('past')
  })

  it('compares instants, not text, so another offset works too', () => {
    expect(sessionState(nineToTen, '2026-03-06T02:30:00Z')).toBe('in-progress')
  })
})

describe('isCancelled', () => {
  it('follows cancelledAt', () => {
    expect(isCancelled({ cancelledAt: null })).toBe(false)
    expect(isCancelled({ cancelledAt: '2026-03-05T17:00:00+07:00' })).toBe(true)
  })
})

describe('changedAfterCutoff', () => {
  it('is true when any change came after the cut-off', () => {
    expect(changedAfterCutoff({ changes: [] })).toBe(false)
    expect(changedAfterCutoff({ changes: [change(false)] })).toBe(false)
    expect(changedAfterCutoff({ changes: [change(false), change(true)] })).toBe(true)
  })
})
