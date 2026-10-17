import type { ChangedBy, Session } from './api'
import { localDate, localTime } from './dates'
import { minutesOf, OPENING } from './layout'

// The values the booking forms hold, and how they become a request. Pure, so it can be tested without a browser.
// No rule is checked here: the API is the only judge, and its 400 or 409 is shown as it comes.

export const DURATIONS = [60, 90]

/** The shortest lesson, so the last start time on offer still ends by closing time. */
const SHORTEST_MIN = Math.min(...DURATIONS)
const TIME_STEP_MIN = 30

/** `("2026-03-07", "13:00", now)` → `"2026-03-07T13:00:00+07:00"`, with the offset of the API's now. */
export function toStartsAt(date: string, time: string, now: string): string {
  const offset = now.endsWith('Z') ? 'Z' : now.slice(-6)
  return `${date}T${time}:00${offset}`
}

/** The start times a form offers: every 30 minutes from opening, as long as a 60-minute lesson ends by closing. */
export function timeOptions(): string[] {
  const opens = minutesOf(`T${OPENING.opens}`)
  const lastStart = minutesOf(`T${OPENING.closes}`) - SHORTEST_MIN
  const times: string[] = []
  for (let minutes = opens; minutes <= lastStart; minutes += TIME_STEP_MIN) {
    times.push(`${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`)
  }
  return times
}

/** A session's length in minutes. */
export function durationOf(session: Pick<Session, 'startsAt' | 'endsAt'>): number {
  return (Date.parse(session.endsAt) - Date.parse(session.startsAt)) / 60_000
}

export interface MoveForm {
  date: string
  time: string
  roomId: string
  durationMin: number
  /** Empty until the receptionist picks one: there is no default. */
  movedBy: ChangedBy | ''
  note: string
}

/** The Move form filled in with the session's own date, time, room and length. */
export function moveFormFrom(session: Session): MoveForm {
  return {
    date: localDate(session.startsAt),
    time: localTime(session.startsAt),
    roomId: session.roomId,
    durationMin: durationOf(session),
    movedBy: '',
    note: '',
  }
}

/** True when the form would move the session to where it already is. */
export function isUnchangedMove(form: MoveForm, session: Session): boolean {
  const own = moveFormFrom(session)
  return (
    form.date === own.date && form.time === own.time && form.roomId === own.roomId && form.durationMin === own.durationMin
  )
}
