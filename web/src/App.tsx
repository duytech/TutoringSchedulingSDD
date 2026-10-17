import { Navigate, Route, Routes } from 'react-router'
import { DayPage } from './pages/DayPage/DayPage'
import { LegacyRedirect } from './pages/LegacyRedirect/LegacyRedirect'
import { TutorDayPage } from './pages/TutorDayPage/TutorDayPage'
import { ViolationsPage } from './pages/ViolationsPage/ViolationsPage'

export default function App() {
  return (
    <Routes>
      <Route path="/rooms" element={<DayPage />} />
      <Route path="/tutors/:tutorId" element={<TutorDayPage />} />
      <Route path="/violations" element={<ViolationsPage />} />
      <Route path="/" element={<LegacyRedirect />} />
      <Route path="*" element={<Navigate to="/rooms" replace />} />
    </Routes>
  )
}
