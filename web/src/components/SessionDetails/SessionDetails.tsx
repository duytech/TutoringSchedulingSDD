import { useState } from 'react'
import type { Room, Session } from '../../api'
import { localTime } from '../../dates'
import { isCancelled, sessionState } from '../../sessionStatus'
import { CancelAttendeeForm } from '../CancelAttendeeForm/CancelAttendeeForm'
import { MoveSessionForm } from '../MoveSessionForm/MoveSessionForm'
import './SessionDetails.css'

interface Props {
  session: Session
  /** The API's now: a session that has started can no longer be changed. */
  now: string
  rooms: Room[]
  /** What the last write did, shown at the top (e.g. the cancel outcome). */
  outcome: string | null
  onCancelled: (session: Session, attendeeId: string) => void
  onMoved: (session: Session) => void
}

const STATUS_TEXT = { booked: null, cancelled: 'cancelled', no_show: 'no show' } as const

/** One session in the panel: who is in it, a Cancel per booked student, and the Move form. */
export function SessionDetails({ session, now, rooms, outcome, onCancelled, onMoved }: Props) {
  const [cancellingAttendeeId, setCancellingAttendeeId] = useState<string | null>(null)
  const lockedReason = isCancelled(session)
    ? 'Cancelled — nothing to change.'
    : sessionState(session, now) === 'upcoming'
      ? null
      : `Started at ${localTime(session.startsAt)} — it can no longer be moved or cancelled.`

  return (
    <div className="details">
      {outcome && (
        <p className="message" role="status">
          {outcome}
        </p>
      )}
      <p className="details__when">
        {localTime(session.startsAt)}–{localTime(session.endsAt)} · {session.tutorId} {session.tutorName} ·{' '}
        {session.roomId}
      </p>

      <ul className="details__students" aria-label="Students">
        {session.attendees.map((attendee) => (
          <li key={attendee.id}>
            <div className="details__student">
              <span>
                {attendee.studentName}
                {STATUS_TEXT[attendee.status] && <span className="details__status"> {STATUS_TEXT[attendee.status]}</span>}
                {attendee.chargeable && <span className="details__status"> · chargeable</span>}
              </span>
              {!lockedReason && attendee.status === 'booked' && cancellingAttendeeId !== attendee.id && (
                <button
                  type="button"
                  className="button"
                  aria-label={`Cancel ${attendee.studentName}`}
                  onClick={() => setCancellingAttendeeId(attendee.id)}
                >
                  Cancel
                </button>
              )}
            </div>
            {!lockedReason && cancellingAttendeeId === attendee.id && (
              <CancelAttendeeForm
                session={session}
                attendee={attendee}
                onCancelled={(updated, attendeeId) => {
                  setCancellingAttendeeId(null)
                  onCancelled(updated, attendeeId)
                }}
                onKeep={() => setCancellingAttendeeId(null)}
              />
            )}
          </li>
        ))}
      </ul>

      {lockedReason ? (
        <p className="message">{lockedReason}</p>
      ) : (
        <>
          <h3 className="details__heading">Move</h3>
          <MoveSessionForm key={session.id} session={session} now={now} rooms={rooms} onMoved={onMoved} />
        </>
      )}
    </div>
  )
}
