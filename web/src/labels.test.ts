import { describe, expect, it } from 'vitest'
import { movedLabel, shortDate } from './labels'

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
