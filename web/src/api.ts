// The shapes of GET /api/sessions, /api/rooms, /api/tutors, /api/tutors/{id}/day and /api/reports/violations,
// as the API sends them.
// Times are local ISO strings with the centre's offset, e.g. "2026-03-06T10:30:00+07:00".

export interface BookingChange {
  kind: 'created' | 'cancelled' | 'moved'
  attendeeId: string | null
  changedAt: string
  changedBy: string | null
  afterCutoff: boolean
  note: string | null
}

export interface SessionAttendee {
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

export interface Session {
  id: string
  tutorId: string
  tutorName: string
  roomId: string
  startsAt: string
  endsAt: string
  /** Null while the session is active. */
  cancelledAt: string | null
  movedToSessionId: string | null
  /** Where a moved session went. It may be on another day. */
  movedTo: MovedTo | null
  legacyViolation: boolean
  attendees: SessionAttendee[]
  changes: BookingChange[]
}

export interface DaySessions {
  date: string
  now: string
  sessions: Session[]
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
  kind: BookingChange['kind']
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
  sessions: Session[]
  changesAfterCutoff: TutorChange[]
}

/** One rule the schedule breaks (ScheduleViolation in the API). Sessions booked in the app have no lesson id. */
export interface ScheduleViolation {
  rule: string
  date: string
  sessionIds: string[]
  lessonIds: string[]
  message: string
}

/** The shape of GET /api/reports/violations. */
export interface ViolationReport {
  violations: ScheduleViolation[]
}

export class ApiError extends Error {
  readonly status: number | null

  constructor(message: string, status: number | null) {
    super(message)
    this.status = status
  }
}

/** Anything a fetch threw, as an ApiError: one already is, anything else has no status. */
export function asApiError(cause: unknown): ApiError {
  return cause instanceof ApiError ? cause : new ApiError(String(cause), null)
}

/** One day's sessions. Without a date, the API's own today (its pinned clock), not the browser's. */
export function fetchDay(date?: string): Promise<DaySessions> {
  return getJson<DaySessions>(date ? `/api/sessions?date=${date}` : '/api/sessions')
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

/** Every rule break between two dates. A missing date leaves that end open. `from` after `to` throws status 400. */
export function fetchViolations(from?: string, to?: string): Promise<ViolationReport> {
  const query = new URLSearchParams()
  if (from) {
    query.set('from', from)
  }
  if (to) {
    query.set('to', to)
  }
  const search = query.toString()
  return getJson<ViolationReport>(search ? `/api/reports/violations?${search}` : '/api/reports/violations')
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
