import { describe, expect, it } from 'vitest'
import { durationOf, isUnchangedMove, moveFormFrom, timeOptions, toStartsAt } from './bookingForm'
import { aSession } from './testData'

describe('toStartsAt', () => {
  it("takes the offset of the API's now", () => {
    expect(toStartsAt('2026-03-07', '13:00', '2026-03-06T10:00:00+07:00')).toBe('2026-03-07T13:00:00+07:00')
  })

  it('keeps a Z offset as Z', () => {
    expect(toStartsAt('2026-03-07', '13:00', '2026-03-06T03:00:00Z')).toBe('2026-03-07T13:00:00Z')
  })
})

describe('timeOptions', () => {
  const times = timeOptions()

  it('starts at opening time and steps by 30 minutes', () => {
    expect(times.slice(0, 3)).toEqual(['09:00', '09:30', '10:00'])
  })

  it('stops at the last start a 60-minute lesson can take before closing (21:30)', () => {
    expect(times.at(-1)).toBe('20:30')
  })
})

describe('moveFormFrom', () => {
  it("fills in the session's date, time, room and length, with no one chosen yet", () => {
    expect(moveFormFrom(aSession())).toEqual({
      date: '2026-03-06',
      time: '10:30',
      roomId: 'R3',
      durationMin: 90,
      movedBy: '',
      note: '',
    })
  })
})

describe('durationOf', () => {
  it('is the minutes between start and end', () => {
    expect(durationOf({ startsAt: '2026-03-06T09:00:00+07:00', endsAt: '2026-03-06T10:00:00+07:00' })).toBe(60)
  })
})

describe('isUnchangedMove', () => {
  const session = aSession()
  const own = moveFormFrom(session)

  it('is true when nothing that moves the session changed', () => {
    expect(isUnchangedMove({ ...own, movedBy: 'family', note: 'flu' }, session)).toBe(true)
  })

  it.each([
    ['date', { date: '2026-03-07' }],
    ['time', { time: '14:00' }],
    ['room', { roomId: 'R4' }],
    ['length', { durationMin: 60 }],
  ])('is false when the %s changed', (_, change) => {
    expect(isUnchangedMove({ ...own, ...change }, session)).toBe(false)
  })
})
