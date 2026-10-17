import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import {
  asApiError,
  fetchDay,
  fetchRooms,
  fetchTutors,
  type ApiError,
  type Room,
  type DaySessions,
  type Tutor,
} from '../../api'
import { tutorColour } from '../../colours'
import { longDate } from '../../dates'
import { DayGrid } from '../../components/DayGrid/DayGrid'
import { ApiErrorMessage } from '../../components/ApiErrorMessage/ApiErrorMessage'
import { PageHeader } from '../../components/PageHeader/PageHeader'
import { TutorLoads } from '../../components/TutorLoads/TutorLoads'
import { useDate } from '../../useDate'
import './DayPage.css'

interface DayData {
  day: DaySessions
  rooms: Room[]
  tutors: Tutor[]
}

/** One day as a grid: a column per room, a card per session. */
export function DayPage() {
  const [date, onGoToDate] = useDate()
  // The last answer stays on screen while the next date loads, so the page does not flash.
  const [data, setData] = useState<DayData | null>(null)
  const [error, setError] = useState<ApiError | null>(null)
  // The date the last answer was for. Undefined until the first one arrives.
  const [loadedDate, setLoadedDate] = useState<string | null>()

  useEffect(() => {
    // Once the date moves on, this answer is stale and is dropped.
    let current = true
    Promise.all([fetchDay(date ?? undefined), fetchRooms(), fetchTutors()])
      .then(([day, rooms, tutors]) => {
        if (current) {
          setData({ day, rooms, tutors })
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
  }, [date])

  const loading = loadedDate !== date
  const day = data?.day ?? null

  return (
    <main className="page">
      <PageHeader
        title={day ? longDate(day.date) : 'Today'}
        date={day?.date ?? null}
        now={day?.now ?? null}
        loading={loading}
        onGoToDate={onGoToDate}
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
        <DayGrid
          day={data.day}
          roomIds={data.rooms.map((room) => room.id)}
          tutorColour={tutorColour}
          onGoToDate={onGoToDate}
        />
      )}
    </main>
  )
}
