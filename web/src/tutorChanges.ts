import type { BookingChange, Session } from './api'

/** A change after the cut-off, with enough of its session to read on its own. */
export interface TutorChange {
  sessionId: string
  sessionStartsAt: string
  roomId: string
  kind: BookingChange['kind']
  attendeeId: string | null
  /** Null for a change to the whole session. */
  studentName: string | null
  changedAt: string
  changedBy: string | null
  note: string | null
}

/**
 * Every change made after the cut-off, oldest first. The API sends the sessions and each session's changes in
 * order, and the sort is stable, so a student's change stays before the session's when they share a time.
 */
export function changesAfterCutoff(sessions: Session[]): TutorChange[] {
  return sessions
    .flatMap((session) =>
      session.changes
        .filter((change) => change.afterCutoff)
        .map((change) => ({
          sessionId: session.id,
          sessionStartsAt: session.startsAt,
          roomId: session.roomId,
          kind: change.kind,
          attendeeId: change.attendeeId,
          studentName: session.attendees.find((attendee) => attendee.id === change.attendeeId)?.studentName ?? null,
          changedAt: change.changedAt,
          changedBy: change.changedBy,
          note: change.note,
        })),
    )
    .sort((first, second) => Date.parse(first.changedAt) - Date.parse(second.changedAt))
}
