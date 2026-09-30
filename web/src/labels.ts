import type { MovedTo, TutorChange, TutorDaySheet } from './api'
import { localDate, localTime } from './dates'

/** "2026-03-11" → "Wed 11 Mar". */
export function shortDate(date: string): string {
  const day = new Date(`${date}T00:00:00Z`)
  const part = (options: Intl.DateTimeFormatOptions) =>
    new Intl.DateTimeFormat('en-GB', { ...options, timeZone: 'UTC' }).format(day)
  return `${part({ weekday: 'short' })} ${day.getUTCDate()} ${part({ month: 'short' })}`
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

/** "Thu 5 Mar 16:00": a local instant with its day. */
function dayAndTime(localIso: string): string {
  return `${shortDate(localDate(localIso))} ${localTime(localIso)}`
}

/**
 * One line of the tutor's "changed after you were told" list, e.g.
 * "16:00 R3 · Do Van Kien cancelled by tutor at 14:40 (tutor sick)". A change to the whole session says "session".
 * The time of the change gets its day when it was not on the lesson's day.
 */
export function changeLine(change: TutorChange): string {
  const who = change.studentName ?? 'session'
  const by = change.changedBy ? ` by ${change.changedBy}` : ''
  const at =
    localDate(change.changedAt) === localDate(change.sessionStartsAt)
      ? localTime(change.changedAt)
      : dayAndTime(change.changedAt)
  const note = change.note ? ` (${change.note})` : ''
  return `${localTime(change.sessionStartsAt)} ${change.roomId} · ${who} ${change.kind}${by} at ${at}${note}`
}

/** "Final since Thu 5 Mar 16:00", or "Not final until …" while the day can still change before the tutor is told. */
export function cutoffLine(sheet: Pick<TutorDaySheet, 'cutoff' | 'final'>): string {
  return `${sheet.final ? 'Final since' : 'Not final until'} ${dayAndTime(sheet.cutoff)}`
}
