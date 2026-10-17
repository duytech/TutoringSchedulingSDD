import { Link } from 'react-router'
import type { DaySessions, Tutor } from '../../api'
import { isCancelled } from '../../sessionStatus'
import { onDate } from '../../useDate'
import './TutorLoads.css'

/** "T1 Ngoc Anh 7 · T2 Pham Duc 2 · T3 Le Thu 1": active sessions per tutor that day, each a link to their sheet. */
export function TutorLoads({ day, tutors }: { day: DaySessions; tutors: Tutor[] }) {
  const active = day.sessions.filter((session) => !isCancelled(session))
  return tutors.map((tutor, index) => (
    <span key={tutor.id}>
      {index > 0 && ' · '}
      <Link to={onDate(`/tutors/${encodeURIComponent(tutor.id)}`, day.date)}>
        {tutor.id} {tutor.name} {active.filter((session) => session.tutorId === tutor.id).length}
      </Link>
    </span>
  ))
}
