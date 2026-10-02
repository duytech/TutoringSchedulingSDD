import { describe, expect, it } from 'vitest'
import type { ScheduleViolation } from './api'
import { groupByDate } from './violations'

function violation(rule: string, date: string): ScheduleViolation {
  return { rule, date, sessionIds: [], lessonIds: [], message: `${rule} on ${date}` }
}

describe('groupByDate', () => {
  it('makes one group per date and keeps the API order inside each', () => {
    const roomOverlap = violation('room-overlap', '2026-03-04')
    const studentOverlap = violation('student-overlap', '2026-03-04')
    const tutorLoad = violation('tutor-load', '2026-03-06')

    expect(groupByDate([roomOverlap, studentOverlap, tutorLoad])).toEqual([
      { date: '2026-03-04', violations: [roomOverlap, studentOverlap] },
      { date: '2026-03-06', violations: [tutorLoad] },
    ])
  })

  it('gives no groups for no violations', () => {
    expect(groupByDate([])).toEqual([])
  })
})
