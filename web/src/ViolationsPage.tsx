import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router'
import { asApiError, fetchViolations, type ApiError, type ScheduleViolation, type ViolationReport } from './api'
import { longDate } from './dates'
import { ruleLabel } from './labels'
import { ApiErrorMessage } from './PageHeader'
import { groupByDate } from './violations'

interface Range {
  from: string | null
  to: string | null
}

/**
 * The range from `?from=` and `?to=` (either may be missing), and a way to change it. Changing it replaces the
 * history entry, so Back leaves the page instead of undoing every date typed.
 */
function useRange(): [Range, (range: Range) => void] {
  const [params, setParams] = useSearchParams()
  const setRange = (range: Range) => {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        for (const name of ['from', 'to'] as const) {
          const value = range[name]
          if (value) {
            next.set(name, value)
          } else {
            next.delete(name)
          }
        }
        return next
      },
      { replace: true },
    )
  }
  return [{ from: params.get('from'), to: params.get('to') }, setRange]
}

/** Every rule the schedule breaks, one section per date, optionally between two dates. */
export function ViolationsPage() {
  const [range, setRange] = useRange()
  const { from, to } = range
  const key = `${from}|${to}`
  // The last answer stays on screen while the next range loads, so the page does not flash.
  const [loadedReport, setLoadedReport] = useState<ViolationReport | null>(null)
  const [error, setError] = useState<ApiError | null>(null)
  // The range the last answer was for. Null until the first one arrives.
  const [loadedKey, setLoadedKey] = useState<string | null>(null)

  useEffect(() => {
    // Once the range moves on, this answer is stale and is dropped.
    let current = true
    fetchViolations(from ?? undefined, to ?? undefined)
      .then((answer) => {
        if (current) {
          setLoadedReport(answer)
          setError(null)
          setLoadedKey(key)
        }
      })
      .catch((cause: unknown) => {
        if (current) {
          setError(asApiError(cause))
          setLoadedKey(key)
        }
      })
    return () => {
      current = false
    }
  }, [key, from, to])

  const loading = loadedKey !== key
  const badRange = error?.status === 400
  // A list from before the range went wrong would not match the inputs, so it is hidden.
  const report = badRange ? null : loadedReport
  const count = report?.violations.length ?? 0

  return (
    <main className="page">
      <header className="top">
        <div>
          <h1>Rule breaks</h1>
          <p className="top__sub">
            {report && <>{count === 1 ? '1 rule break' : `${count} rule breaks`} · </>}
            <Link to="/rooms">← All rooms</Link>
            {loading && <span className="top__loading"> · loading…</span>}
          </p>
        </div>
        <RangeForm range={range} onChange={setRange} />
      </header>

      {badRange ? (
        <p className="message message--error" role="alert">
          From must be on or before To.
        </p>
      ) : (
        error && <ApiErrorMessage error={error} />
      )}
      {report && count === 0 && (
        <p className="message">{from || to ? 'No rule breaks in this range.' : 'No rule breaks.'}</p>
      )}
      {report && <ViolationList violations={report.violations} />}
    </main>
  )
}

function RangeForm({ range, onChange }: { range: Range; onChange: (range: Range) => void }) {
  return (
    <form className="range" onSubmit={(event) => event.preventDefault()}>
      <label>
        From{' '}
        <input
          type="date"
          value={range.from ?? ''}
          onChange={(event) => onChange({ ...range, from: event.target.value || null })}
        />
      </label>
      <label>
        To{' '}
        <input
          type="date"
          value={range.to ?? ''}
          onChange={(event) => onChange({ ...range, to: event.target.value || null })}
        />
      </label>
      <button type="button" onClick={() => onChange({ from: null, to: null })} disabled={!range.from && !range.to}>
        Clear
      </button>
    </form>
  )
}

function ViolationList({ violations }: { violations: ScheduleViolation[] }) {
  return (
    <div className="violations">
      {groupByDate(violations).map((day) => (
        <section key={day.date}>
          <h2>{longDate(day.date)}</h2>
          <ul>
            {day.violations.map((violation) => (
              <li key={`${violation.rule}|${violation.message}`} className="violation">
                <span className="violation__rule">{ruleLabel(violation.rule)}</span>
                <div>
                  <p className="violation__message">{violation.message}</p>
                  {violation.lessonIds.length > 0 && (
                    <p className="violation__lessons">{violation.lessonIds.join(' · ')}</p>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  )
}
