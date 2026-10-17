// The shapes of GET /api/sessions, /api/rooms, /api/tutors, /api/students, /api/tutors/{id}/day and
// /api/reports/violations, as the API sends them, and the three writes: book, move and cancel.
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

/** One entry of GET /api/students. */
export interface Student {
  id: string
  name: string
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

/** Who asked for a cancel or a move. Only a late family cancel is chargeable. */
export type ChangedBy = 'family' | 'tutor' | 'centre'

/** The body of POST /api/sessions. */
export interface CreateSessionRequest {
  tutorId: string
  roomId: string
  /** A local time with its offset: "2026-03-07T13:00:00+07:00". */
  startsAt: string
  durationMin: number
  studentIds: string[]
}

/** The body of POST /api/sessions/{id}/move. */
export interface MoveSessionRequest {
  startsAt: string
  roomId: string
  durationMin: number
  movedBy: ChangedBy
  note: string | null
}

/** The body of POST /api/sessions/{id}/attendees/{attendeeId}/cancel. */
export interface CancelAttendeeRequest {
  cancelledBy: ChangedBy
  note: string | null
}

/**
 * The answer to a write, as a form shows it. A refusal is returned, not thrown: the form shows it and keeps what was
 * typed. Only an API that cannot be reached, or an answer no form expects, throws an ApiError.
 */
export type WriteResult =
  | { kind: 'ok'; session: Session }
  /** 400: the messages by field name (startsAt, studentIds, …). */
  | { kind: 'invalid'; errors: Record<string, string[]> }
  /** 409: every rule the write breaks, at once. */
  | { kind: 'conflict'; conflicts: ScheduleViolation[] }
  /** 404: the session (or the attendee) is gone. */
  | { kind: 'not-found' }

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

/** Every student, ordered by name. */
export function fetchStudents(): Promise<Student[]> {
  return getJson<Student[]>('/api/students')
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

/** Books a session. A 201 answers with the new session. */
export function createSession(request: CreateSessionRequest): Promise<WriteResult> {
  return postJson('/api/sessions', request)
}

/** Moves a session. A 201 answers with the new session; the old one is cancelled and points at it. */
export function moveSession(sessionId: string, request: MoveSessionRequest): Promise<WriteResult> {
  return postJson(`/api/sessions/${encodeURIComponent(sessionId)}/move`, request)
}

/** Cancels one student's place. A 200 answers with the session, cancelled too when no one is left. */
export function cancelAttendee(
  sessionId: string,
  attendeeId: string,
  request: CancelAttendeeRequest,
): Promise<WriteResult> {
  const path = `/api/sessions/${encodeURIComponent(sessionId)}/attendees/${encodeURIComponent(attendeeId)}/cancel`
  return postJson(path, request)
}

async function postJson(url: string, body: unknown): Promise<WriteResult> {
  const response = await send(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (response.ok) {
    return { kind: 'ok', session: (await response.json()) as Session }
  }
  if (response.status === 404) {
    return { kind: 'not-found' }
  }
  if (response.status === 400 || response.status === 409) {
    const problem = (await response.json()) as {
      errors?: Record<string, string[]>
      conflicts?: ScheduleViolation[]
    }
    if (response.status === 400 && problem.errors) {
      return { kind: 'invalid', errors: problem.errors }
    }
    if (response.status === 409 && problem.conflicts) {
      return { kind: 'conflict', conflicts: problem.conflicts }
    }
  }
  throw new ApiError(`The API answered ${response.status}.`, response.status)
}

async function getJson<TResult>(url: string): Promise<TResult> {
  const response = await send(url)
  if (!response.ok) {
    throw new ApiError(`The API answered ${response.status}.`, response.status)
  }
  return (await response.json()) as TResult
}

/** `fetch`, with a network error or a gateway error as "Cannot reach the API." */
async function send(url: string, init?: RequestInit): Promise<Response> {
  let response: Response
  try {
    response = await fetch(url, init)
  } catch {
    throw new ApiError('Cannot reach the API.', null)
  }
  // A gateway error comes from the dev server's proxy, not the API: the API is not running.
  if ([502, 503, 504].includes(response.status)) {
    throw new ApiError('Cannot reach the API.', null)
  }
  return response
}
