// @vitest-environment jsdom
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, useLocation } from 'react-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Session } from '../../api'
import { fakeFetch, type FakeRoutes } from '../../fakeFetch'
import { anAttendee, aSession, NOW, ROOMS, STUDENTS, TUTORS } from '../../testData'
import { DayPage } from './DayPage'

const l020 = aSession()

function dayRoutes(sessionsByDate: Record<string, Session[]>): FakeRoutes {
  const routes: FakeRoutes = {
    'GET /api/rooms': { status: 200, body: ROOMS },
    'GET /api/tutors': { status: 200, body: TUTORS },
    'GET /api/students': { status: 200, body: STUDENTS },
  }
  for (const [date, sessions] of Object.entries(sessionsByDate)) {
    routes[`GET /api/sessions?date=${date}`] = { status: 200, body: { date, now: NOW, sessions } }
  }
  return routes
}

function ShowSearch() {
  return <output aria-label="search">{useLocation().search}</output>
}

function renderPage() {
  render(
    <MemoryRouter initialEntries={['/rooms?date=2026-03-06']}>
      <DayPage />
      <ShowSearch />
    </MemoryRouter>,
  )
}

const dayReads = (seen: { method: string; url: string }[]) =>
  seen.filter((request) => request.url === '/api/sessions?date=2026-03-06').length

describe('DayPage', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('opens the create form from + New session', async () => {
    fakeFetch(dayRoutes({ '2026-03-06': [l020] }))
    renderPage()

    await userEvent.setup().click(await screen.findByRole('button', { name: '+ New session' }))

    expect(screen.getByRole('complementary', { name: 'New session' })).toBeInTheDocument()
    expect(screen.getByLabelText('Date')).toHaveValue('2026-03-06')
  })

  it('opens a session from its card', async () => {
    fakeFetch(dayRoutes({ '2026-03-06': [l020] }))
    renderPage()

    await userEvent.setup().click(await screen.findByRole('button', { name: '10:30–12:00' }))

    const panel = screen.getByRole('complementary', { name: 'Session' })
    expect(within(panel).getByRole('button', { name: 'Cancel Bui An Nhien' })).toBeInTheDocument()
  })

  it('after a booking on another day, goes to that day with the new session open', async () => {
    const booked = aSession({
      id: 'new',
      tutorId: 'T2',
      roomId: 'R4',
      startsAt: '2026-03-07T13:00:00+07:00',
      endsAt: '2026-03-07T14:00:00+07:00',
      attendees: [anAttendee({ id: 'attendee-new', studentName: 'Vu Ha My' })],
    })
    fakeFetch({
      ...dayRoutes({ '2026-03-06': [l020], '2026-03-07': [booked] }),
      'POST /api/sessions': { status: 201, body: booked },
    })
    renderPage()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: '+ New session' }))
    const date = screen.getByLabelText('Date')
    await user.clear(date)
    await user.type(date, '2026-03-07')
    await user.selectOptions(screen.getByLabelText('Student'), 'Vu Ha My')
    await user.click(screen.getByRole('button', { name: 'Book' }))

    expect(await screen.findByRole('heading', { name: 'Saturday 7 March 2026' })).toBeInTheDocument()
    expect(screen.getByRole('status', { name: 'search' })).toHaveTextContent('?date=2026-03-07')
    const panel = screen.getByRole('complementary', { name: 'Session' })
    expect(within(panel).getByText('Vu Ha My')).toBeInTheDocument()
  })

  it('after a cancel, reads the day again and shows what the cancel did', async () => {
    const cancelled = aSession({
      cancelledAt: NOW,
      attendees: [anAttendee({ status: 'cancelled', chargeable: true })],
      changes: [
        {
          kind: 'cancelled',
          attendeeId: 'attendee-1',
          changedAt: NOW,
          changedBy: 'family',
          afterCutoff: true,
          note: null,
        },
      ],
    })
    let cancelDone = false
    const seen = fakeFetch({
      ...dayRoutes({}),
      'GET /api/sessions?date=2026-03-06': () => ({
        status: 200,
        body: { date: '2026-03-06', now: NOW, sessions: [cancelDone ? cancelled : l020] },
      }),
      'POST /api/sessions/session-1/attendees/attendee-1/cancel': () => {
        cancelDone = true
        return { status: 200, body: cancelled }
      },
    })
    renderPage()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: '10:30–12:00' }))
    await user.click(screen.getByRole('button', { name: 'Cancel Bui An Nhien' }))
    await user.selectOptions(screen.getByLabelText('Cancelled by'), 'family')
    await user.click(screen.getByRole('button', { name: 'Confirm cancel' }))

    const outcome = await screen.findByText(
      'Bui An Nhien cancelled · chargeable · after the tutor was told · the session is cancelled',
    )
    expect(outcome).toBeInTheDocument()
    await vi.waitFor(() => expect(dayReads(seen)).toBe(2))
    await vi.waitFor(() => expect(screen.getByRole('article')).toHaveClass('card--cancelled'))
  })

  it('closes a session when the day is moved by hand', async () => {
    fakeFetch(dayRoutes({ '2026-03-06': [l020], '2026-03-07': [] }))
    renderPage()
    const user = userEvent.setup()

    await user.click(await screen.findByRole('button', { name: '10:30–12:00' }))
    await user.click(screen.getByRole('button', { name: '→' }))

    expect(screen.queryByRole('complementary', { name: 'Session' })).not.toBeInTheDocument()
  })
})
