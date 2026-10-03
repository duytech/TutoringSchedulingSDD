import type { CSSProperties } from 'react'
import type { Session } from './api'
import { localDate, localTime } from './dates'
import { movedLabel } from './labels'
import type { Placement } from './layout'
import { changedAfterCutoff, isCancelled, sessionState } from './sessionStatus'

const FLAG_TITLE = 'Loaded from the export; breaks a centre rule. See /api/reports/violations.'

interface Props {
  session: Session
  /** Where the card sits in the room grid. Without it, the card is a row of the tutor's list and names the room. */
  placement?: Placement
  tutorColour: string
  /** The date the grid shows, to tell a move within the day from a move to another day. */
  shownDate: string
  /** The API's now, to tell past, in-progress and upcoming apart. */
  now: string
  onGoToDate: (date: string) => void
}

export function SessionCard({ session, placement, tutorColour, shownDate, now, onGoToDate }: Props) {
  const flagged = session.legacyViolation || session.attendees.some((attendee) => attendee.legacyViolation)
  const lateChanges = session.changes.filter((change) => change.afterCutoff)
  const moved = session.movedTo ? movedLabel(shownDate, session.movedTo) : null
  const movedToDate = moved?.otherDate
  const classes = [
    'card',
    `card--${sessionState(session, now)}`,
    isCancelled(session) ? 'card--cancelled' : '',
    placement ? '' : 'card--row',
  ].filter(Boolean)

  const style = {
    ...(placement && {
      gridColumn: placement.column + 2,
      gridRow: `${placement.rowStart + 2} / span ${placement.rowSpan}`,
      width: `calc(${100 / placement.lanes}% - 4px)`,
      marginLeft: `calc(${(100 * placement.lane) / placement.lanes}% + 2px)`,
    }),
    '--tutor': tutorColour,
  } as CSSProperties

  return (
    <article className={classes.join(' ')} style={style} aria-label={`${session.tutorName} in ${session.roomId}`}>
      <header className="card__head">
        <span className="card__time">
          {localTime(session.startsAt)}–{localTime(session.endsAt)}
        </span>
        <span className="card__tutor">{placement ? `${session.tutorId} ${session.tutorName}` : session.roomId}</span>
        {flagged && (
          <span className="card__flag" title={FLAG_TITLE}>
            ⚑
          </span>
        )}
      </header>
      <ul className="card__students">
        {session.attendees.map((attendee) => (
          <li key={attendee.id} className={attendee.status === 'cancelled' ? 'student--cancelled' : undefined}>
            {attendee.studentName}
            {attendee.lessonId && <span className="card__lesson"> {attendee.lessonId}</span>}
          </li>
        ))}
      </ul>
      {moved &&
        (movedToDate ? (
          <button
            type="button"
            className="card__moved"
            title={moved.text}
            onClick={() => onGoToDate(movedToDate)}
          >
            {moved.text}
          </button>
        ) : (
          <div className="card__moved" title={moved.text}>
            {moved.text}
          </div>
        ))}
      {changedAfterCutoff(session) && (
        <div
          className="card__badge"
          title={lateChanges
            .map((change) => {
              // A change without an attendee is about the whole session (the last student out cancels it too).
              const who = session.attendees.find((attendee) => attendee.id === change.attendeeId)?.studentName ?? 'Session'
              const when = `${localDate(change.changedAt)} at ${localTime(change.changedAt)}`
              return `${who}: ${change.kind} by ${change.changedBy ?? 'unknown'} on ${when}`
            })
            .join('\n')}
        >
          changed after tutor was told
        </div>
      )}
    </article>
  )
}
