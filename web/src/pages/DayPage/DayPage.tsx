import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router'
import {
  asApiError,
  fetchDay,
  fetchRooms,
  fetchStudents,
  fetchTutors,
  type ApiError,
  type Room,
  type DaySessions,
  type Session,
  type Student,
  type Tutor,
} from '../../api'
import { tutorColour } from '../../colours'
import { localDate, longDate } from '../../dates'
import { cancelOutcomeLine } from '../../labels'
import { BookingPanel } from '../../components/BookingPanel/BookingPanel'
import { CreateSessionForm } from '../../components/CreateSessionForm/CreateSessionForm'
import { DayGrid } from '../../components/DayGrid/DayGrid'
import { ApiErrorMessage } from '../../components/ApiErrorMessage/ApiErrorMessage'
import { PageHeader } from '../../components/PageHeader/PageHeader'
import { SessionDetails } from '../../components/SessionDetails/SessionDetails'
import { TutorLoads } from '../../components/TutorLoads/TutorLoads'
import { useDate } from '../../useDate'
import './DayPage.css'

interface DayData {
  day: DaySessions
  rooms: Room[]
  tutors: Tutor[]
  students: Student[]
}

/**
 * What the booking panel shows. A session panel keeps the session it was opened with, so it can show it before the
 * day has reloaded; once it has, the day's own copy is shown.
 */
type Panel = { kind: 'create' } | { kind: 'session'; session: Session; outcome: string | null }

/** One day as a grid: a column per room, a card per session, and a side panel to book, move and cancel. */
export function DayPage() {
  const [date, goToDate] = useDate()
  // The last answer stays on screen while the next date loads, so the page does not flash.
  const [data, setData] = useState<DayData | null>(null)
  const [error, setError] = useState<ApiError | null>(null)
  // The date the last answer was for. Undefined until the first one arrives.
  const [loadedDate, setLoadedDate] = useState<string | null>()
  const [panel, setPanel] = useState<Panel | null>(null)
  // Bumped after a write, so the day is read again.
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    // Once the date moves on, this answer is stale and is dropped.
    let current = true
    Promise.all([fetchDay(date ?? undefined), fetchRooms(), fetchTutors(), fetchStudents()])
      .then(([day, rooms, tutors, students]) => {
        if (current) {
          setData({ day, rooms, tutors, students })
          setError(null)
          setLoadedDate(date)
        }
      })
      .catch((cause: unknown) => {
        if (current) {
          setError(asApiError(cause))
          setLoadedDate(date)
        }
      })
    return () => {
      current = false
    }
  }, [date, reloadKey])

  const loading = loadedDate !== date
  const day = data?.day ?? null
  const closePanel = useCallback(() => setPanel(null), [])

  // Moving to another day by hand closes a session: it belongs to the day it was opened on.
  const goToDateByHand = (next: string | null) => {
    if (panel?.kind === 'session') {
      setPanel(null)
    }
    goToDate(next)
  }

  // After a write: read the day again, and show the session the API sent back on its own date.
  const showWritten = (session: Session, outcome: string | null) => {
    setPanel({ kind: 'session', session, outcome })
    setReloadKey((key) => key + 1)
    const writtenDate = localDate(session.startsAt)
    if (writtenDate !== day?.date) {
      goToDate(writtenDate)
    }
  }

  const panelSession =
    panel?.kind === 'session'
      ? (day?.sessions.find((session) => session.id === panel.session.id) ?? panel.session)
      : null

  return (
    <main className="page">
      <PageHeader
        title={day ? longDate(day.date) : 'Today'}
        date={day?.date ?? null}
        now={day?.now ?? null}
        loading={loading}
        onGoToDate={goToDateByHand}
        actions={
          data && (
            <button type="button" onClick={() => setPanel({ kind: 'create' })}>
              + New session
            </button>
          )
        }
      >
        {data && (
          <>
            <TutorLoads day={data.day} tutors={data.tutors} /> · <Link to="/violations">Violations</Link>
          </>
        )}
      </PageHeader>

      {error && <ApiErrorMessage error={error} />}
      {day?.sessions.length === 0 && <p className="message">No sessions on this day.</p>}
      {data && (
        <div className="day">
          <div className="day__grid">
            <DayGrid
              day={data.day}
              roomIds={data.rooms.map((room) => room.id)}
              tutorColour={tutorColour}
              onGoToDate={goToDateByHand}
              onSelect={(session) => setPanel({ kind: 'session', session, outcome: null })}
              selectedSessionId={panelSession?.id ?? null}
            />
          </div>

          {panel?.kind === 'create' && (
            <BookingPanel title="New session" onClose={closePanel}>
              <CreateSessionForm
                date={data.day.date}
                now={data.day.now}
                rooms={data.rooms}
                tutors={data.tutors}
                students={data.students}
                onBooked={(session) => showWritten(session, null)}
              />
            </BookingPanel>
          )}
          {panelSession && (
            <BookingPanel title="Session" onClose={closePanel}>
              <SessionDetails
                key={panelSession.id}
                session={panelSession}
                now={data.day.now}
                rooms={data.rooms}
                outcome={panel?.kind === 'session' ? panel.outcome : null}
                onCancelled={(session, attendeeId) => showWritten(session, cancelOutcomeLine(session, attendeeId))}
                onMoved={(session) => showWritten(session, null)}
              />
            </BookingPanel>
          )}
        </div>
      )}
    </main>
  )
}
