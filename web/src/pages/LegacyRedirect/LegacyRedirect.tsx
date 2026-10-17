import { Navigate, useSearchParams } from 'react-router'
import { onDate } from '../../useDate'

/** Links from before the routes (`/?tutor=T1&date=…`, `/?date=…`) still land on the right page. */
export function LegacyRedirect() {
  const [params] = useSearchParams()
  const tutor = params.get('tutor')
  const path = tutor ? `/tutors/${encodeURIComponent(tutor)}` : '/rooms'
  return <Navigate to={onDate(path, params.get('date'))} replace />
}
