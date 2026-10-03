import type { Session } from './api'

export type SessionState = 'past' | 'in-progress' | 'upcoming'

/**
 * Where a session is relative to the API's now (its pinned clock), not the browser's. About time only: a
 * cancelled session still has one. Half-open [start, end): a lesson that ends at 10:00 is over at 10:00.
 */
export function sessionState(session: Pick<Session, 'startsAt' | 'endsAt'>, now: string): SessionState {
  const at = Date.parse(now)
  if (at >= Date.parse(session.endsAt)) return 'past'
  if (at >= Date.parse(session.startsAt)) return 'in-progress'
  return 'upcoming'
}

export function isCancelled(session: Pick<Session, 'cancelledAt'>): boolean {
  return session.cancelledAt !== null
}

/** True when any change to the session came after the tutor was told (16:00 the day before). */
export function changedAfterCutoff(session: Pick<Session, 'changes'>): boolean {
  return session.changes.some((change) => change.afterCutoff)
}
