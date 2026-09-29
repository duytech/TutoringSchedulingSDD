// The shape of GET /api/schedule, as the API sends it (ScheduleDayView in the API).
// Times are local ISO strings with the centre's offset, e.g. "2026-03-06T10:30:00+07:00".

export type SessionState = 'past' | 'in-progress' | 'upcoming'

export interface ScheduleChange {
  kind: 'created' | 'cancelled' | 'moved'
  attendeeId: string | null
  changedAt: string
  changedBy: string | null
  afterCutoff: boolean
  note: string | null
}

export interface ScheduleAttendee {
  id: string
  studentId: string
  studentName: string
  lessonId: string | null
  status: 'booked' | 'cancelled' | 'no_show'
  cancelledAt: string | null
  cancelledBy: string | null
  chargeable: boolean
  legacyViolation: boolean
  note: string | null
}

export interface MovedTo {
  id: string
  startsAt: string
  roomId: string
}

export interface ScheduleSession {
  id: string
  tutorId: string
  tutorName: string
  roomId: string
  startsAt: string
  endsAt: string
  durationMin: number
  state: SessionState
  cancelled: boolean
  cancelledAt: string | null
  movedToSessionId: string | null
  /** Where a moved session went. It may be on another day. */
  movedTo: MovedTo | null
  legacyViolation: boolean
  changedAfterCutoff: boolean
  attendees: ScheduleAttendee[]
  changes: ScheduleChange[]
}

export interface RoomDay {
  id: string
  sessionIds: string[]
}

export interface TutorDay {
  id: string
  name: string
  sessionIds: string[]
}

export interface ScheduleDay {
  date: string
  now: string
  sessions: ScheduleSession[]
  rooms: RoomDay[]
  tutors: TutorDay[]
}

export class ApiError extends Error {
  readonly status: number | null

  constructor(message: string, status: number | null) {
    super(message)
    this.status = status
  }
}

/** One day's schedule. Without a date, the API's own today (its pinned clock), not the browser's. */
export async function fetchDay(date?: string): Promise<ScheduleDay> {
  let response: Response
  try {
    response = await fetch(date ? `/api/schedule?date=${date}` : '/api/schedule')
  } catch {
    throw new ApiError('Cannot reach the API.', null)
  }
  // A gateway error comes from the dev server's proxy, not the API: the API is not running.
  if ([502, 503, 504].includes(response.status)) {
    throw new ApiError('Cannot reach the API.', null)
  }
  if (!response.ok) {
    throw new ApiError(`The API answered ${response.status}.`, response.status)
  }
  return (await response.json()) as ScheduleDay
}
