// @vitest-environment jsdom
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { fakeFetch, lastRequest } from '../../fakeFetch'
import { aSession, NOW, ROOMS } from '../../testData'
import { MoveSessionForm } from './MoveSessionForm'

/** L028 as seeded: 2026-03-07 09:00–10:30, T3 in R3. */
const l028 = aSession({ id: 'l028', startsAt: '2026-03-07T09:00:00+07:00', endsAt: '2026-03-07T10:30:00+07:00' })

function renderForm() {
  const onMoved = vi.fn()
  render(<MoveSessionForm session={l028} now={NOW} rooms={ROOMS} onMoved={onMoved} />)
  return onMoved
}

describe('MoveSessionForm', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it("is filled in with the session's date, time, room and length", () => {
    renderForm()

    expect(screen.getByLabelText('Date')).toHaveValue('2026-03-07')
    expect(screen.getByLabelText('Start')).toHaveValue('09:00')
    expect(screen.getByLabelText('Room')).toHaveValue('R3')
    expect(screen.getByLabelText('Length')).toHaveValue('90')
    expect(screen.getByLabelText('Moved by')).toHaveValue('')
  })

  it('keeps Move disabled until the time, room or length changes', async () => {
    renderForm()
    const user = userEvent.setup()
    const move = screen.getByRole('button', { name: 'Move' })

    await user.selectOptions(screen.getByLabelText('Moved by'), 'family')
    expect(move).toBeDisabled()

    await user.selectOptions(screen.getByLabelText('Start'), '14:00')
    expect(move).toBeEnabled()
  })

  it('sends the new slot and who asked, and hands the new session up', async () => {
    const moved = aSession({ id: 'new', startsAt: '2026-03-07T14:00:00+07:00' })
    const seen = fakeFetch({ 'POST /api/sessions/l028/move': { status: 201, body: moved } })
    const onMoved = renderForm()
    const user = userEvent.setup()

    await user.selectOptions(screen.getByLabelText('Start'), '14:00')
    await user.selectOptions(screen.getByLabelText('Moved by'), 'family')
    await user.type(screen.getByLabelText('Note'), 'exam moved')
    await user.click(screen.getByRole('button', { name: 'Move' }))

    expect(lastRequest(seen, 'POST', '/api/sessions/l028/move').body).toEqual({
      startsAt: '2026-03-07T14:00:00+07:00',
      roomId: 'R3',
      durationMin: 90,
      movedBy: 'family',
      note: 'exam moved',
    })
    expect(onMoved).toHaveBeenCalledWith(moved)
  })

  it('sends nothing until someone is chosen as who asked', async () => {
    const seen = fakeFetch({})
    renderForm()
    const user = userEvent.setup()

    await user.selectOptions(screen.getByLabelText('Start'), '14:00')
    await user.click(screen.getByRole('button', { name: 'Move' }))

    expect(seen).toEqual([])
  })

  it('lists the conflicts of a 409', async () => {
    fakeFetch({
      'POST /api/sessions/l028/move': {
        status: 409,
        body: {
          conflicts: [
            { rule: 'tutor-load', date: '2026-03-07', sessionIds: [], lessonIds: [], message: 'T3 would have 7.' },
          ],
        },
      },
    })
    const onMoved = renderForm()
    const user = userEvent.setup()

    await user.selectOptions(screen.getByLabelText('Start'), '14:00')
    await user.selectOptions(screen.getByLabelText('Moved by'), 'tutor')
    await user.click(screen.getByRole('button', { name: 'Move' }))

    const list = await screen.findByRole('list', { name: 'Conflicts' })
    expect(within(list).getByRole('listitem')).toHaveTextContent('Tutor loadT3 would have 7.')
    expect(onMoved).not.toHaveBeenCalled()
  })
})
