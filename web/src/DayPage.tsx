import { useCallback } from 'react'
import { Link } from 'react-router'
import { fetchDay, fetchRooms, fetchTutors, type ScheduleDay, type Tutor } from './api'
import { tutorColour } from './colours'
import { longDate } from './dates'
import { DayGrid } from './DayGrid'
import { ApiErrorMessage, PageHeader } from './PageHeader'
import { onDate, useDate } from './useDate'
import { useLoad } from './useLoad'

/** One day as a grid: a column per room, a card per session. */
export function DayPage() {
  const [date, onGoToDate] = useDate()
  const load = useCallback(async () => {
    const [day, rooms, tutors] = await Promise.all([fetchDay(date ?? undefined), fetchRooms(), fetchTutors()])
    return { day, rooms, tutors }
  }, [date])
  const { data, error, loading } = useLoad(date ?? '', load)
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
        {data && <TutorLoads day={data.day} tutors={data.tutors} />}
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

/** "T1 Ngoc Anh 7 · T2 Pham Duc 2 · T3 Le Thu 1": active sessions per tutor that day, each a link to their sheet. */
function TutorLoads({ day, tutors }: { day: ScheduleDay; tutors: Tutor[] }) {
  const active = day.sessions.filter((session) => !session.cancelled)
  return tutors.map((tutor, index) => (
    <span key={tutor.id}>
      {index > 0 && ' · '}
      <Link to={onDate(`/tutors/${encodeURIComponent(tutor.id)}`, day.date)}>
        {tutor.id} {tutor.name} {active.filter((session) => session.tutorId === tutor.id).length}
      </Link>
    </span>
  ))
}
