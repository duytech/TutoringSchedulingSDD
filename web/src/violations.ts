import type { ScheduleViolation } from './api'

export interface ViolationDay {
  date: string
  violations: ScheduleViolation[]
}

/** The report as one group per date, in the order the dates first appear. The API's order holds inside each group. */
export function groupByDate(violations: ScheduleViolation[]): ViolationDay[] {
  const days: ViolationDay[] = []
  for (const violation of violations) {
    const day = days.find((existing) => existing.date === violation.date)
    if (day) {
      day.violations.push(violation)
    } else {
      days.push({ date: violation.date, violations: [violation] })
    }
  }
  return days
}
