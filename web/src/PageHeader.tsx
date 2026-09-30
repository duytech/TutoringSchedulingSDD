import type { ReactNode } from 'react'
import type { ApiError } from './api'
import { addDays, localDate, localTime } from './dates'

interface Props {
  title: string
  /** The date shown, once it has loaded. The day buttons wait for it. */
  date: string | null
  /** The API's "now", once it has loaded. */
  now: string | null
  loading: boolean
  onGoToDate: (date: string | null) => void
  /** The rest of the line under the title. */
  children?: ReactNode
}

/** The title, the line under it, and ← / Today / → to move between days. */
export function PageHeader({ title, date, now, loading, onGoToDate, children }: Props) {
  const isToday = date !== null && now !== null && localDate(now) === date
  return (
    <header className="top">
      <div>
        <h1>{title}</h1>
        <p className="top__sub">
          {isToday && <>Now {localTime(now)} · </>}
          {children}
          {loading && <span className="top__loading"> · loading…</span>}
        </p>
      </div>
      <nav className="top__nav" aria-label="Day">
        <button type="button" onClick={() => date && onGoToDate(addDays(date, -1))} disabled={!date}>
          ←
        </button>
        <button type="button" onClick={() => onGoToDate(null)}>
          Today
        </button>
        <button type="button" onClick={() => date && onGoToDate(addDays(date, 1))} disabled={!date}>
          →
        </button>
      </nav>
    </header>
  )
}

export function ApiErrorMessage({ error }: { error: ApiError }) {
  return (
    <p className="message message--error" role="alert">
      Cannot reach the API. Is it running (<code>dotnet run --project src/BrightPath.Api</code>)?
      {error.status !== null && <> The API answered {error.status}.</>}
    </p>
  )
}
