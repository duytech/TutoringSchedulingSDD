import { useState } from 'react'
import { cancelAttendee, type ChangedBy, type Session, type SessionAttendee } from '../../api'
import { fieldError, useWrite } from '../../useWrite'
import { ChangedBySelect } from '../ChangedBySelect/ChangedBySelect'
import { Field } from '../Field/Field'
import { WriteProblemMessage } from '../WriteProblemMessage/WriteProblemMessage'
import './CancelAttendeeForm.css'

interface Props {
  session: Session
  attendee: SessionAttendee
  onCancelled: (session: Session, attendeeId: string) => void
  /** Closes the form without cancelling. */
  onKeep: () => void
}

const FIELDS = ['cancelledBy', 'note']

/** Cancels one student's place. Who asked is required: only a late family cancel is chargeable. */
export function CancelAttendeeForm({ session, attendee, onCancelled, onKeep }: Props) {
  const [cancelledBy, setCancelledBy] = useState<ChangedBy | ''>('')
  const [note, setNote] = useState('')
  const { sending, problem, runWrite } = useWrite((updated) => onCancelled(updated, attendee.id))

  const cancel = (chosen: ChangedBy) =>
    runWrite(() => cancelAttendee(session.id, attendee.id, { cancelledBy: chosen, note: note || null }))

  return (
    <form
      className="form cancel"
      aria-label={`Cancel ${attendee.studentName}`}
      onSubmit={(event) => {
        event.preventDefault()
        if (cancelledBy) {
          void cancel(cancelledBy)
        }
      }}
    >
      <ChangedBySelect
        label="Cancelled by"
        value={cancelledBy}
        onChange={setCancelledBy}
        error={fieldError(problem, 'cancelledBy')}
      />
      <Field label="Note" error={fieldError(problem, 'note')}>
        {(id) => <input id={id} value={note} maxLength={500} onChange={(event) => setNote(event.target.value)} />}
      </Field>
      <WriteProblemMessage problem={problem} fields={FIELDS} />
      <div className="form__actions">
        <button type="submit" disabled={sending}>
          Confirm cancel
        </button>
        <button type="button" onClick={onKeep}>
          Keep
        </button>
      </div>
    </form>
  )
}
