import type { CSSProperties } from 'react'
import type { ScheduleSession } from './api'
import { localDate, localTime } from './dates'
import { movedLabel } from './labels'
import type { Placement } from './layout'

const FLAG_TITLE = 'Loaded from the export; breaks a centre rule. See /api/reports/violations.'

interface Props {
  session: ScheduleSession
  /** Where the card sits in the room grid. Without it, the card is a row of the tutor's list and names the room. */
  placement?: Placement
  tutorColour: string
  /** The date the grid shows, to tell a move within the day from a move to another day. */
  shownDate: string
  onGoToDate: (date: string) => void
}

export function SessionCard({ session, placement, tutorColour, shownDate, onGoToDate }: Props) {
  const flagged = session.legacyViolation || session.attendees.some((a) => a.legacyViolation)
  const lateChanges = session.changes.filter((c) => c.afterCutoff)
  const moved = session.movedTo ? movedLabel(shownDate, session.movedTo) : null
  const movedToDate = moved?.otherDate
  const classes = [
    'card',
    `card--${session.state}`,
    session.cancelled ? 'card--cancelled' : '',
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
        {session.attendees.map((a) => (
          <li key={a.id} className={a.status === 'cancelled' ? 'student--cancelled' : undefined}>
            {a.studentName}
            {a.lessonId && <span className="card__lesson"> {a.lessonId}</span>}
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
      {session.changedAfterCutoff && (
        <div
          className="card__badge"
          title={lateChanges
            .map((c) => {
              // A change without an attendee is about the whole session (the last student out cancels it too).
              const who = session.attendees.find((a) => a.id === c.attendeeId)?.studentName ?? 'Session'
              const when = `${localDate(c.changedAt)} at ${localTime(c.changedAt)}`
              return `${who}: ${c.kind} by ${c.changedBy ?? 'unknown'} on ${when}`
            })
            .join('\n')}
        >
          changed after tutor was told
        </div>
      )}
    </article>
  )
}
