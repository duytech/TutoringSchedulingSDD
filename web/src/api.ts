// The shapes of GET /api/schedule, /api/rooms, /api/tutors and /api/tutors/{id}/day, as the API sends them.
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

export interface ScheduleDay {
  date: string
  now: string
  sessions: ScheduleSession[]
}

/** One entry of GET /api/rooms. */
export interface Room {
  id: string
}

/** One entry of GET /api/tutors. */
export interface Tutor {
  id: string
  name: string
  subject: string
}

/** A change after the cut-off, with enough of its session to read on its own (TutorChangeView in the API). */
export interface TutorChange {
  sessionId: string
  sessionStartsAt: string
  roomId: string
  kind: ScheduleChange['kind']
  attendeeId: string | null
  /** Null for a change to the whole session. */
  studentName: string | null
  changedAt: string
  changedBy: string | null
  note: string | null
}

/** The shape of GET /api/tutors/{id}/day (TutorDaySheetView in the API). */
export interface TutorDaySheet {
  tutorId: string
  tutorName: string
  date: string
  now: string
  /** 16:00 the day before: from then on, the tutor counts as told. */
  cutoff: string
  final: boolean
  sessions: ScheduleSession[]
  changesAfterCutoff: TutorChange[]
}

export class ApiError extends Error {
  readonly status: number | null

  constructor(message: string, status: number | null) {
    super(message)
    this.status = status
  }
}

/** One day's schedule. Without a date, the API's own today (its pinned clock), not the browser's. */
export function fetchDay(date?: string): Promise<ScheduleDay> {
  return getJson<ScheduleDay>(date ? `/api/schedule?date=${date}` : '/api/schedule')
}

/** Every room, ordered by id. */
export function fetchRooms(): Promise<Room[]> {
  return getJson<Room[]>('/api/rooms')
}

/** Every tutor, ordered by id. */
export function fetchTutors(): Promise<Tutor[]> {
  return getJson<Tutor[]>('/api/tutors')
}

/** One tutor's day. An unknown tutor throws an ApiError with status 404. */
export function fetchTutorDay(tutorId: string, date?: string): Promise<TutorDaySheet> {
  const path = `/api/tutors/${encodeURIComponent(tutorId)}/day`
  return getJson<TutorDaySheet>(date ? `${path}?date=${date}` : path)
}

async function getJson<TResult>(url: string): Promise<TResult> {
  let response: Response
  try {
    response = await fetch(url)
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
  return (await response.json()) as TResult
}
