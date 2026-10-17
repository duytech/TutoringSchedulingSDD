import { useState } from 'react'
import { createSession, type Room, type Session, type Student, type Tutor } from '../../api'
import { DURATIONS, timeOptions, toStartsAt } from '../../bookingForm'
import { fieldError, useWrite } from '../../useWrite'
import { Field } from '../Field/Field'
import { WriteProblemMessage } from '../WriteProblemMessage/WriteProblemMessage'
import './CreateSessionForm.css'

interface Props {
  /** The date the grid shows: the form starts on it. */
  date: string
  /** The API's now, for the offset of the new start time. */
  now: string
  rooms: Room[]
  tutors: Tutor[]
  students: Student[]
  onBooked: (session: Session) => void
}

const FIELDS = ['startsAt', 'durationMin', 'roomId', 'tutorId', 'studentIds']

/** Books a new session. The API checks every rule; a refusal is shown and the values stay. */
export function CreateSessionForm({ date, now, rooms, tutors, students, onBooked }: Props) {
  const [day, setDay] = useState(date)
  const [time, setTime] = useState(timeOptions()[0] ?? '')
  const [durationMin, setDurationMin] = useState(DURATIONS[0] ?? 60)
  const [roomId, setRoomId] = useState(rooms[0]?.id ?? '')
  const [tutorId, setTutorId] = useState(tutors[0]?.id ?? '')
  const [studentId, setStudentId] = useState('')
  const [secondStudentId, setSecondStudentId] = useState('')
  const { sending, problem, runWrite } = useWrite(onBooked)

  const book = () =>
    runWrite(() =>
      createSession({
        tutorId,
        roomId,
        startsAt: toStartsAt(day, time, now),
        durationMin,
        studentIds: [studentId, secondStudentId].filter(Boolean),
      }),
    )

  const studentOptions = students.map((student) => (
    <option key={student.id} value={student.id}>
      {student.name}
    </option>
  ))

  return (
    <form
      className="form"
      aria-label="New session"
      onSubmit={(event) => {
        event.preventDefault()
        void book()
      }}
    >
      <div className="form__row">
        <Field label="Date">
          {(id) => <input id={id} type="date" required value={day} onChange={(event) => setDay(event.target.value)} />}
        </Field>
        <Field label="Start" error={fieldError(problem, 'startsAt')}>
          {(id) => (
            <select id={id} value={time} onChange={(event) => setTime(event.target.value)}>
              {timeOptions().map((option) => (
                <option key={option}>{option}</option>
              ))}
            </select>
          )}
        </Field>
        <Field label="Length" error={fieldError(problem, 'durationMin')}>
          {(id) => (
            <select id={id} value={durationMin} onChange={(event) => setDurationMin(Number(event.target.value))}>
              {DURATIONS.map((minutes) => (
                <option key={minutes} value={minutes}>
                  {minutes} min
                </option>
              ))}
            </select>
          )}
        </Field>
      </div>

      <div className="form__row">
        <Field label="Room" error={fieldError(problem, 'roomId')}>
          {(id) => (
            <select id={id} value={roomId} onChange={(event) => setRoomId(event.target.value)}>
              {rooms.map((room) => (
                <option key={room.id}>{room.id}</option>
              ))}
            </select>
          )}
        </Field>
        <Field label="Tutor" error={fieldError(problem, 'tutorId')}>
          {(id) => (
            <select id={id} value={tutorId} onChange={(event) => setTutorId(event.target.value)}>
              {tutors.map((tutor) => (
                <option key={tutor.id} value={tutor.id}>
                  {tutor.id} {tutor.name}
                </option>
              ))}
            </select>
          )}
        </Field>
      </div>

      <Field label="Student" error={fieldError(problem, 'studentIds')}>
        {(id) => (
          <select id={id} required value={studentId} onChange={(event) => setStudentId(event.target.value)}>
            <option value="">Choose a student</option>
            {studentOptions}
          </select>
        )}
      </Field>
      <Field label="Second student (exam pair)">
        {(id) => (
          <select id={id} value={secondStudentId} onChange={(event) => setSecondStudentId(event.target.value)}>
            <option value="">None</option>
            {studentOptions}
          </select>
        )}
      </Field>

      <WriteProblemMessage problem={problem} fields={FIELDS} />
      <div className="form__actions">
        <button type="submit" disabled={sending}>
          Book
        </button>
      </div>
    </form>
  )
}
