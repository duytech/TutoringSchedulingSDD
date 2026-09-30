import { Navigate, Route, Routes, useSearchParams } from 'react-router'
import { DayPage } from './DayPage'
import { TutorDayPage } from './TutorDayPage'
import { onDate } from './useDate'

export default function App() {
  return (
    <Routes>
      <Route path="/rooms" element={<DayPage />} />
      <Route path="/tutors/:tutorId" element={<TutorDayPage />} />
      <Route path="/" element={<LegacyRedirect />} />
      <Route path="*" element={<Navigate to="/rooms" replace />} />
    </Routes>
  )
}

/** Links from before the routes (`/?tutor=T1&date=…`, `/?date=…`) still land on the right page. */
function LegacyRedirect() {
  const [params] = useSearchParams()
  const tutor = params.get('tutor')
  const path = tutor ? `/tutors/${encodeURIComponent(tutor)}` : '/rooms'
  return <Navigate to={onDate(path, params.get('date'))} replace />
}
