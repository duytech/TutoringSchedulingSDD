import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router'
import { asApiError, fetchTutorDay, type ApiError, type TutorDaySheet } from './api'
import { tutorColour } from './colours'
import { longDate } from './dates'
import { cutoffLine } from './labels'
import { ApiErrorMessage, PageHeader } from './PageHeader'
import { TutorSheet } from './TutorSheet'
import { onDate, useDate } from './useDate'

/** One tutor's day: their lessons by time, and what changed after they were told. */
export function TutorDayPage() {
  // The route is /tutors/:tutorId, so the id is always there.
  const { tutorId: tutor = '' } = useParams()
  const [date, onGoToDate] = useDate()
  // The last answer stays on screen while the next one loads, so the page does not flash.
  const [loadedSheet, setLoadedSheet] = useState<TutorDaySheet | null>(null)
  const [error, setError] = useState<ApiError | null>(null)
  // The tutor and date the last answer was for. Null until the first one arrives.
  const [loadedFor, setLoadedFor] = useState<{ tutor: string; date: string | null } | null>(null)

  useEffect(() => {
    // Once the tutor or the date moves on, this answer is stale and is dropped.
    let current = true
    fetchTutorDay(tutor, date ?? undefined)
      .then((answer) => {
        if (current) {
          setLoadedSheet(answer)
          setError(null)
          setLoadedFor({ tutor, date })
        }
      })
      .catch((cause: unknown) => {
        if (current) {
          setError(asApiError(cause))
          setLoadedFor({ tutor, date })
        }
      })
    return () => {
      current = false
    }
  }, [tutor, date])

  const loading = loadedFor?.tutor !== tutor || loadedFor.date !== date
  // A sheet left over from before the tutor changed would show the wrong tutor, so only a match counts.
  const sheet = loadedSheet?.tutorId === tutor ? loadedSheet : null

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
