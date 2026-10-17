import { useState } from 'react'
import { moveSession, type ChangedBy, type Room, type Session } from '../../api'
import { DURATIONS, isUnchangedMove, moveFormFrom, timeOptions, toStartsAt, type MoveForm } from '../../bookingForm'
import { fieldError, useWrite } from '../../useWrite'
import { ChangedBySelect } from '../ChangedBySelect/ChangedBySelect'
import { Field } from '../Field/Field'
import { WriteProblemMessage } from '../WriteProblemMessage/WriteProblemMessage'
import './MoveSessionForm.css'

interface Props {
  session: Session
  /** The API's now, for the offset of the new start time. */
  now: string
  rooms: Room[]
  onMoved: (session: Session) => void
}

const FIELDS = ['startsAt', 'roomId', 'durationMin', 'movedBy', 'note']

/** Moves the whole session to a new time, room or length. The tutor stays. */
export function MoveSessionForm({ session, now, rooms, onMoved }: Props) {
  const [form, setForm] = useState<MoveForm>(() => moveFormFrom(session))
  const { sending, problem, runWrite } = useWrite(onMoved)
  const changeForm = (fields: Partial<MoveForm>) => setForm((current) => ({ ...current, ...fields }))

  const move = (movedBy: ChangedBy) =>
    runWrite(() =>
      moveSession(session.id, {
        startsAt: toStartsAt(form.date, form.time, now),
        roomId: form.roomId,
        durationMin: form.durationMin,
        movedBy,
        note: form.note || null,
      }),
    )

  return (
    <form
      className="form"
      aria-label="Move"
      onSubmit={(event) => {
        event.preventDefault()
        if (form.movedBy) {
          void move(form.movedBy)
        }
      }}
    >
      <div className="form__row">
        <Field label="Date">
          {(id) => (
            <input
              id={id}
              type="date"
              required
              value={form.date}
              onChange={(event) => changeForm({ date: event.target.value })}
            />
          )}
        </Field>
        <Field label="Start" error={fieldError(problem, 'startsAt')}>
          {(id) => (
            <select id={id} value={form.time} onChange={(event) => changeForm({ time: event.target.value })}>
              {timeOptions().map((option) => (
                <option key={option}>{option}</option>
              ))}
            </select>
          )}
        </Field>
      </div>

      <div className="form__row">
        <Field label="Room" error={fieldError(problem, 'roomId')}>
          {(id) => (
            <select id={id} value={form.roomId} onChange={(event) => changeForm({ roomId: event.target.value })}>
              {rooms.map((room) => (
                <option key={room.id}>{room.id}</option>
              ))}
            </select>
          )}
        </Field>
        <Field label="Length" error={fieldError(problem, 'durationMin')}>
          {(id) => (
            <select
              id={id}
              value={form.durationMin}
              onChange={(event) => changeForm({ durationMin: Number(event.target.value) })}
            >
              {DURATIONS.map((minutes) => (
                <option key={minutes} value={minutes}>
                  {minutes} min
                </option>
              ))}
            </select>
          )}
        </Field>
      </div>

      <ChangedBySelect
        label="Moved by"
        value={form.movedBy}
        onChange={(movedBy) => changeForm({ movedBy })}
        error={fieldError(problem, 'movedBy')}
      />
      <Field label="Note" error={fieldError(problem, 'note')}>
        {(id) => (
          <input
            id={id}
            value={form.note}
            maxLength={500}
            onChange={(event) => changeForm({ note: event.target.value })}
          />
        )}
      </Field>

      <WriteProblemMessage problem={problem} fields={FIELDS} />
      <div className="form__actions">
        <button type="submit" disabled={sending || isUnchangedMove(form, session)}>
          Move
        </button>
      </div>
    </form>
  )
}
