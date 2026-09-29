import { useEffect, useMemo, useState } from 'react'
import { ApiError, fetchDay, type ScheduleDay } from './api'
import { addDays, localDate, localTime, longDate } from './dates'
import { DayGrid } from './DayGrid'

const TUTOR_COLOURS = ['#2f6fdb', '#d9822b', '#2a9d6f', '#9b51e0', '#c2410c', '#0e7490']

function dateFromUrl(): string | null {
  return new URLSearchParams(window.location.search).get('date')
}

/** Keeps the shown date in the URL, so a reload stays on it. No date means the API's today. */
function writeDateToUrl(date: string | null) {
  const url = new URL(window.location.href)
  if (date) {
    url.searchParams.set('date', date)
  } else {
    url.searchParams.delete('date')
  }
  window.history.replaceState(null, '', url)
}

export default function App() {
  const [date, setDate] = useState<string | null>(dateFromUrl)
  const [day, setDay] = useState<ScheduleDay | null>(null)
  const [error, setError] = useState<ApiError | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    let current = true
    // The previous day stays on screen until the new one arrives, so the page does not flash.
    fetchDay(date ?? undefined)
      .then((d) => {
        if (current) {
          setDay(d)
          setError(null)
        }
      })
      .catch((e: unknown) => {
        if (current) {
          setError(e instanceof ApiError ? e : new ApiError(String(e), null))
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
  }, [date])

  const go = (next: string | null) => {
    if (next === date) {
      return
    }
    writeDateToUrl(next)
    setLoading(true)
    setDate(next)
  }

  const tutorColours = useMemo(
    () => new Map((day?.tutors ?? []).map((t, i) => [t.id, TUTOR_COLOURS[i % TUTOR_COLOURS.length]])),
    [day],
  )

  const isToday = day !== null && localDate(day.now) === day.date

  return (
    <main className="page">
      <header className="top">
        <div>
          <h1>{day ? longDate(day.date) : 'Today'}</h1>
          <p className="top__sub">
            {isToday && <>Now {localTime(day.now)} · </>}
            {day && tutorLoads(day)}
            {loading && <span className="top__loading"> · loading…</span>}
          </p>
        </div>
        <nav className="top__nav" aria-label="Day">
          <button type="button" onClick={() => day && go(addDays(day.date, -1))} disabled={!day}>
            ←
          </button>
          <button type="button" onClick={() => go(null)}>
            Today
          </button>
          <button type="button" onClick={() => day && go(addDays(day.date, 1))} disabled={!day}>
            →
          </button>
        </nav>
      </header>

      {error && (
        <p className="message message--error" role="alert">
          Cannot reach the API. Is it running (<code>dotnet run --project src/BrightPath.Api</code>)?
          {error.status !== null && <> The API answered {error.status}.</>}
        </p>
      )}

      {day && day.sessions.length === 0 && <p className="message">No sessions on this day.</p>}
      {day && <DayGrid day={day} tutorColours={tutorColours} />}
    </main>
  )
}

/** "T1 Ngoc Anh 7 · T2 Pham Duc 2 · T3 Le Thu 1": active sessions per tutor that day. */
function tutorLoads(day: ScheduleDay): string {
  const active = new Set(day.sessions.filter((s) => !s.cancelled).map((s) => s.id))
  return day.tutors
    .map((t) => `${t.id} ${t.name} ${t.sessionIds.filter((id) => active.has(id)).length}`)
    .join(' · ')
}
