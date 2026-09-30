import { useEffect, useState, type MouseEvent } from 'react'
import {
  ApiError,
  fetchDay,
  fetchRooms,
  fetchTutorDay,
  fetchTutors,
  type Room,
  type ScheduleDay,
  type Tutor,
  type TutorDaySheet,
} from './api'
import { addDays, localDate, localTime, longDate } from './dates'
import { DayGrid } from './DayGrid'
import { cutoffLine } from './labels'
import { TutorSheet } from './TutorSheet'

const TUTOR_COLOURS = ['#2f6fdb', '#d9822b', '#2a9d6f', '#9b51e0', '#c2410c', '#0e7490']

/** A tutor keeps one colour on the grid and on their own sheet: T1 is the first colour, T2 the second, and so on. */
function tutorColour(tutorId: string): string {
  const tutorNumber = Number(tutorId.replace(/\D/g, '')) || 1
  return TUTOR_COLOURS[(tutorNumber - 1) % TUTOR_COLOURS.length]
}

/** What the page shows: a date (none means the API's today), and a tutor's sheet or, without one, the room grid. */
interface Place {
  date: string | null
  tutor: string | null
}

function placeFromUrl(): Place {
  const params = new URLSearchParams(window.location.search)
  return { date: params.get('date'), tutor: params.get('tutor') }
}

function urlOf({ date, tutor }: Place): string {
  const url = new URL(window.location.href)
  url.search = ''
  if (tutor) {
    url.searchParams.set('tutor', tutor)
  }
  if (date) {
    url.searchParams.set('date', date)
  }
  return url.toString()
}

interface Link {
  href: string
  onClick: (event: MouseEvent) => void
}

type Shown =
  | { kind: 'day'; day: ScheduleDay; rooms: Room[]; tutors: Tutor[] }
  | { kind: 'tutor'; sheet: TutorDaySheet }

function load({ date, tutor }: Place): Promise<Shown> {
  return tutor
    ? fetchTutorDay(tutor, date ?? undefined).then((sheet) => ({ kind: 'tutor', sheet }) as const)
    : Promise.all([fetchDay(date ?? undefined), fetchRooms(), fetchTutors()]).then(
        ([day, rooms, tutors]) => ({ kind: 'day', day, rooms, tutors }) as const,
      )
}

export default function App() {
  const [place, setPlace] = useState<Place>(placeFromUrl)
  const [shown, setShown] = useState<Shown | null>(null)
  const [error, setError] = useState<ApiError | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let current = true
    // The previous page stays on screen until the new one arrives, so the page does not flash.
    load(place)
      .then((loaded) => {
        if (current) {
          setShown(loaded)
          setError(null)
        }
      })
      .catch((cause: unknown) => {
        if (current) {
          setError(cause instanceof ApiError ? cause : new ApiError(String(cause), null))
        }
      })
      .finally(() => {
        if (current) {
          setLoading(false)
        }
      })
    return () => {
      current = false
    }
  }, [place])

  // Keeps the place in the URL, so a reload stays on it.
  const goTo = (next: Place) => {
    if (next.date === place.date && next.tutor === place.tutor) {
      return
    }
    window.history.replaceState(null, '', urlOf(next))
    setLoading(true)
    setPlace(next)
  }
  const go = (date: string | null) => goTo({ ...place, date })

  /** A real link (it opens in a new tab too) that a plain click follows without a reload. */
  const linkTo = (next: Place): Link => ({
    href: urlOf(next),
    onClick: (event: MouseEvent) => {
      if (!event.metaKey && !event.ctrlKey && !event.shiftKey && event.button === 0) {
        event.preventDefault()
        goTo(next)
      }
    },
  })

  const date = shown?.kind === 'day' ? shown.day.date : shown?.kind === 'tutor' ? shown.sheet.date : null
  const now = shown?.kind === 'day' ? shown.day.now : shown?.kind === 'tutor' ? shown.sheet.now : null
  const isToday = date !== null && now !== null && localDate(now) === date
  // A page left over from before the tutor changed would show the wrong tutor, so only a match counts.
  const sheet = shown?.kind === 'tutor' && shown.sheet.tutorId === place.tutor ? shown.sheet : null
  const dayView = shown?.kind === 'day' && place.tutor === null ? shown : null
  const day = dayView?.day ?? null
  const unknownTutor = place.tutor !== null && error?.status === 404

  return (
    <main className="page">
      <header className="top">
        <div>
          <h1>
            {sheet
              ? `${sheet.tutorName} (${sheet.tutorId}) · ${longDate(sheet.date)}`
              : date && !place.tutor
                ? longDate(date)
                : 'Today'}
          </h1>
          <p className="top__sub">
            {isToday && now && <>Now {localTime(now)} · </>}
            {dayView && (
              <TutorLoads
                day={dayView.day}
                tutors={dayView.tutors}
                linkTo={(tutor) => linkTo({ date: dayView.day.date, tutor })}
              />
            )}
            {sheet && (
              <>
                {cutoffLine(sheet)} · <a {...linkTo({ date: sheet.date, tutor: null })}>← All rooms</a>
              </>
            )}
            {loading && <span className="top__loading"> · loading…</span>}
          </p>
        </div>
        <nav className="top__nav" aria-label="Day">
          <button type="button" onClick={() => date && go(addDays(date, -1))} disabled={!date}>
            ←
          </button>
          <button type="button" onClick={() => go(null)}>
            Today
          </button>
          <button type="button" onClick={() => date && go(addDays(date, 1))} disabled={!date}>
            →
          </button>
        </nav>
      </header>

      {unknownTutor ? (
        <p className="message message--error" role="alert">
          No tutor {place.tutor}. <a {...linkTo({ date: place.date, tutor: null })}>← All rooms</a>
        </p>
      ) : (
        error && (
          <p className="message message--error" role="alert">
            Cannot reach the API. Is it running (<code>dotnet run --project src/BrightPath.Api</code>)?
            {error.status !== null && <> The API answered {error.status}.</>}
          </p>
        )
      )}

      {day?.sessions.length === 0 && <p className="message">No sessions on this day.</p>}
      {dayView && (
        <DayGrid
          day={dayView.day}
          roomIds={dayView.rooms.map((room) => room.id)}
          tutorColour={tutorColour}
          onGoToDate={go}
        />
      )}
      {sheet && <TutorSheet sheet={sheet} tutorColour={tutorColour(sheet.tutorId)} onGoToDate={go} />}
    </main>
  )
}

/** "T1 Ngoc Anh 7 · T2 Pham Duc 2 · T3 Le Thu 1": active sessions per tutor that day, each a link to their sheet. */
function TutorLoads({ day, tutors, linkTo }: { day: ScheduleDay; tutors: Tutor[]; linkTo: (tutor: string) => Link }) {
  const active = day.sessions.filter((session) => !session.cancelled)
  return tutors.map((tutor, index) => (
    <span key={tutor.id}>
      {index > 0 && ' · '}
      <a {...linkTo(tutor.id)}>
        {tutor.id} {tutor.name} {active.filter((session) => session.tutorId === tutor.id).length}
      </a>
    </span>
  ))
}
