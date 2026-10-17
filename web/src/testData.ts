// Test data: small builders for a valid session or attendee, and the reference lists as seeded.
import type { Session, SessionAttendee } from './api'

// Only what a test cares about is changed from the defaults.

export function anAttendee(overrides: Partial<SessionAttendee> = {}): SessionAttendee {
  return {
    id: 'attendee-1',
    studentId: 'student-1',
    studentName: 'Bui An Nhien',
    lessonId: 'L020',
    status: 'booked',
    cancelledAt: null,
    cancelledBy: null,
    chargeable: false,
    legacyViolation: false,
    note: null,
    ...overrides,
  }
}

/** L020 as seeded: 2026-03-06 10:30–12:00, T3 in R3, one booked student. */
export function aSession(overrides: Partial<Session> = {}): Session {
  return {
    id: 'session-1',
    tutorId: 'T3',
    tutorName: 'Le Thu',
    roomId: 'R3',
    startsAt: '2026-03-06T10:30:00+07:00',
    endsAt: '2026-03-06T12:00:00+07:00',
    cancelledAt: null,
    movedToSessionId: null,
    movedTo: null,
    legacyViolation: false,
    attendees: [anAttendee()],
    changes: [],
    ...overrides,
  }
}

export const NOW = '2026-03-06T10:00:00+07:00'

export const ROOMS = ['R1', 'R2', 'R3', 'R4', 'R5', 'R6'].map((roomId) => ({ id: roomId }))

export const TUTORS = [
  { id: 'T1', name: 'Ngoc Anh', subject: 'Maths' },
  { id: 'T2', name: 'Pham Duc', subject: 'English' },
  { id: 'T3', name: 'Le Thu', subject: 'Physics' },
]

export const STUDENTS = [
  { id: 'student-1', name: 'Bui An Nhien' },
  { id: 'student-2', name: 'Do Van Kien' },
  { id: 'student-6', name: 'Vu Ha My' },
]
