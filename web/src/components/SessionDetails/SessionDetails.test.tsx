// @vitest-environment jsdom
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import type { Session } from '../../api'
import { fakeFetch, lastRequest } from '../../fakeFetch'
import { anAttendee, aSession, NOW, ROOMS } from '../../testData'
import { SessionDetails } from './SessionDetails'

const CANCEL_URL = '/api/sessions/session-1/attendees/attendee-1/cancel'

function renderDetails(session: Session, outcome: string | null = null) {
  const onCancelled = vi.fn()
  const onMoved = vi.fn()
  render(
    <SessionDetails
      session={session}
      now={NOW}
      rooms={ROOMS}
      outcome={outcome}
      onCancelled={onCancelled}
      onMoved={onMoved}
    />,
  )
  return { onCancelled, onMoved }
}

describe('SessionDetails', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offers Cancel for a booked student only, and the Move form', () => {
    renderDetails(
      aSession({
        attendees: [anAttendee(), anAttendee({ id: 'attendee-2', studentName: 'Do Van Kien', status: 'cancelled' })],
      }),
    )

    expect(screen.getByRole('button', { name: 'Cancel Bui An Nhien' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Cancel Do Van Kien' })).not.toBeInTheDocument()
    expect(screen.getByRole('form', { name: 'Move' })).toBeInTheDocument()
  })

  it('sends nothing when Confirm cancel is pressed before choosing who asked', async () => {
    const seen = fakeFetch({})
    renderDetails(aSession())
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Cancel Bui An Nhien' }))
    await user.click(screen.getByRole('button', { name: 'Confirm cancel' }))

    expect(seen).toEqual([])
  })

  it('cancels with who asked and the note, and hands the session up', async () => {
    const cancelled = aSession({ cancelledAt: NOW, attendees: [anAttendee({ status: 'cancelled', chargeable: true })] })
    const seen = fakeFetch({ [`POST ${CANCEL_URL}`]: { status: 200, body: cancelled } })
    const { onCancelled } = renderDetails(aSession())
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Cancel Bui An Nhien' }))
    const cancelForm = screen.getByRole('form', { name: 'Cancel Bui An Nhien' })
    await user.selectOptions(within(cancelForm).getByLabelText('Cancelled by'), 'family')
    await user.type(within(cancelForm).getByLabelText('Note'), 'flu')
    await user.click(screen.getByRole('button', { name: 'Confirm cancel' }))

    expect(lastRequest(seen, 'POST', CANCEL_URL).body).toEqual({ cancelledBy: 'family', note: 'flu' })
    expect(onCancelled).toHaveBeenCalledWith(cancelled, 'attendee-1')
  })

  it('says the session is gone on a 404', async () => {
    fakeFetch({ [`POST ${CANCEL_URL}`]: { status: 404, body: { title: 'Not found' } } })
    renderDetails(aSession())
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: 'Cancel Bui An Nhien' }))
    await user.selectOptions(screen.getByLabelText('Cancelled by'), 'centre')
    await user.click(screen.getByRole('button', { name: 'Confirm cancel' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('This session no longer exists.')
  })

  it('offers no change for a session that has started (L018)', () => {
    renderDetails(aSession({ startsAt: '2026-03-06T09:00:00+07:00', endsAt: '2026-03-06T10:00:00+07:00' }))

    expect(screen.getByText('Started at 09:00 — it can no longer be moved or cancelled.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /^Cancel / })).not.toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'Move' })).not.toBeInTheDocument()
  })

  it('offers no change for a cancelled session', () => {
    renderDetails(aSession({ cancelledAt: NOW, attendees: [anAttendee({ status: 'cancelled' })] }))

    expect(screen.getByText('Cancelled — nothing to change.')).toBeInTheDocument()
    expect(screen.queryByRole('form', { name: 'Move' })).not.toBeInTheDocument()
  })

  it('shows the outcome of the last write at the top', () => {
    renderDetails(aSession(), 'Bui An Nhien cancelled · chargeable')

    expect(screen.getByRole('status')).toHaveTextContent('Bui An Nhien cancelled · chargeable')
  })
})
