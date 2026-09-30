import { useSearchParams } from 'react-router'

/**
 * The page's date from `?date=` (none means the API's today), and a way to move it. Moving the date replaces the
 * history entry, so Back returns to the page you came from rather than stepping back through every day.
 */
export function useDate(): [string | null, (date: string | null) => void] {
  const [params, setParams] = useSearchParams()
  const goToDate = (date: string | null) => {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (date) {
          next.set('date', date)
        } else {
          next.delete('date')
        }
        return next
      },
      { replace: true },
    )
  }
  return [params.get('date'), goToDate]
}

/** `/rooms` or `/tutors/T1` on a date, or on the API's today when there is none. */
export function onDate(path: string, date: string | null): string {
  return date ? `${path}?date=${date}` : path
}
