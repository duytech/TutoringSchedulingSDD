import { useCallback } from 'react'
import { Link, useParams } from 'react-router'
import { fetchTutorDay } from './api'
import { tutorColour } from './colours'
import { longDate } from './dates'
import { cutoffLine } from './labels'
import { ApiErrorMessage, PageHeader } from './PageHeader'
import { TutorSheet } from './TutorSheet'
import { onDate, useDate } from './useDate'
import { useLoad } from './useLoad'

/** One tutor's day: their lessons by time, and what changed after they were told. */
export function TutorDayPage() {
  // The route is /tutors/:tutorId, so the id is always there.
  const { tutorId: tutor = '' } = useParams()
  const [date, onGoToDate] = useDate()
  const load = useCallback(() => fetchTutorDay(tutor, date ?? undefined), [tutor, date])
  const { data, error, loading } = useLoad(`${tutor}/${date ?? ''}`, load)
  // A sheet left over from before the tutor changed would show the wrong tutor, so only a match counts.
  const sheet = data?.tutorId === tutor ? data : null

  return (
    <main className="page">
      <PageHeader
        title={sheet ? `${sheet.tutorName} (${sheet.tutorId}) · ${longDate(sheet.date)}` : 'Today'}
        date={sheet?.date ?? null}
        now={sheet?.now ?? null}
        loading={loading}
        onGoToDate={onGoToDate}
      >
        {sheet && (
          <>
            {cutoffLine(sheet)} · <Link to={onDate('/rooms', sheet.date)}>← All rooms</Link>
          </>
        )}
      </PageHeader>

      {error?.status === 404 ? (
        <p className="message message--error" role="alert">
          No tutor {tutor}. <Link to={onDate('/rooms', date)}>← All rooms</Link>
        </p>
      ) : (
        error && <ApiErrorMessage error={error} />
      )}
      {sheet && <TutorSheet sheet={sheet} tutorColour={tutorColour(sheet.tutorId)} onGoToDate={onGoToDate} />}
    </main>
  )
}
