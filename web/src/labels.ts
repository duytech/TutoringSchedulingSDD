import type { MovedTo } from './api'
import { localDate, localTime } from './dates'

/** "2026-03-11" → "Wed 11 Mar". */
export function shortDate(date: string): string {
  const d = new Date(`${date}T00:00:00Z`)
  const part = (options: Intl.DateTimeFormatOptions) =>
    new Intl.DateTimeFormat('en-GB', { ...options, timeZone: 'UTC' }).format(d)
  return `${part({ weekday: 'short' })} ${d.getUTCDate()} ${part({ month: 'short' })}`
}

/**
 * Where a moved session went, as its card says it: "moved → 16:00 R4" on the same day, or
 * "moved → Wed 11 Mar 16:00 R4" when it went to another day (`otherDate` is then the date to link to).
 */
export function movedLabel(shownDate: string, movedTo: MovedTo): { text: string; otherDate: string | null } {
  const date = localDate(movedTo.startsAt)
  const where = `${localTime(movedTo.startsAt)} ${movedTo.roomId}`
  return date === shownDate
    ? { text: `moved → ${where}`, otherDate: null }
    : { text: `moved → ${shortDate(date)} ${where}`, otherDate: date }
}
