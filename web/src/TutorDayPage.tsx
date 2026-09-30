import { useCallback } from 'react'
import { fetchTutorDay } from './api'
import { tutorColour } from './colours'
import { longDate } from './dates'
import { cutoffLine } from './labels'
import { ApiErrorMessage, PageHeader } from './PageHeader'
import type { Link, Place } from './place'
import { TutorSheet } from './TutorSheet'
import { useLoad } from './useLoad'

interface Props {
  tutor: string
  /** None means the API's today. */
  date: string | null
  onGoToDate: (date: string | null) => void
  linkTo: (place: Place) => Link
}

/** One tutor's day: their lessons by time, and what changed after they were told. */
export function TutorDayPage({ tutor, date, onGoToDate, linkTo }: Props) {
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
            {cutoffLine(sheet)} · <a {...linkTo({ date: sheet.date, tutor: null })}>← All rooms</a>
          </>
        )}
      </PageHeader>

      {error?.status === 404 ? (
        <p className="message message--error" role="alert">
          No tutor {tutor}. <a {...linkTo({ date, tutor: null })}>← All rooms</a>
        </p>
      ) : (
        error && <ApiErrorMessage error={error} />
      )}
      {sheet && <TutorSheet sheet={sheet} tutorColour={tutorColour(sheet.tutorId)} onGoToDate={onGoToDate} />}
    </main>
  )
}
