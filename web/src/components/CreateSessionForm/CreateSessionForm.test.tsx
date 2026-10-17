// @vitest-environment jsdom
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { fakeFetch, lastRequest, type FakeAnswer } from '../../fakeFetch'
import { aSession, NOW, ROOMS, STUDENTS, TUTORS } from '../../testData'
import { CreateSessionForm } from './CreateSessionForm'

function renderForm() {
  const onBooked = vi.fn()
  render(
    <CreateSessionForm
      date="2026-03-06"
      now={NOW}
      rooms={ROOMS}
      tutors={TUTORS}
      students={STUDENTS}
      onBooked={onBooked}
    />,
  )
  return onBooked
}

/** Fills in row 1 of the spec: T2, R4, 2026-03-07 13:00, 60 min, Vu Ha My. */
async function fillRowOne(user: ReturnType<typeof userEvent.setup>) {
  const date = screen.getByLabelText('Date')
  await user.clear(date)
  await user.type(date, '2026-03-07')
  await user.selectOptions(screen.getByLabelText('Start'), '13:00')
  await user.selectOptions(screen.getByLabelText('Room'), 'R4')
  await user.selectOptions(screen.getByLabelText('Tutor'), 'T2')
  await user.selectOptions(screen.getByLabelText('Student'), 'Vu Ha My')
}

const conflict = (rule: string, message: string) => ({ rule, date: '2026-03-07', sessionIds: [], lessonIds: [], message })

describe('CreateSessionForm', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('books with the local start time and its offset, and hands the new session up', async () => {
    const booked = aSession({ id: 'new', startsAt: '2026-03-07T13:00:00+07:00' })
    const seen = fakeFetch({ 'POST /api/sessions': { status: 201, body: booked } })
    const onBooked = renderForm()
    const user = userEvent.setup()

    await fillRowOne(user)
    await user.click(screen.getByRole('button', { name: 'Book' }))

    expect(lastRequest(seen, 'POST', '/api/sessions').body).toEqual({
      tutorId: 'T2',
      roomId: 'R4',
      startsAt: '2026-03-07T13:00:00+07:00',
      durationMin: 60,
      studentIds: ['student-6'],
    })
    expect(onBooked).toHaveBeenCalledWith(booked)
  })

  it('sends both students of an exam pair', async () => {
    const seen = fakeFetch({ 'POST /api/sessions': { status: 201, body: aSession() } })
    renderForm()
    const user = userEvent.setup()

    await user.selectOptions(screen.getByLabelText('Student'), 'Bui An Nhien')
    await user.selectOptions(screen.getByLabelText('Second student (exam pair)'), 'Do Van Kien')
    await user.click(screen.getByRole('button', { name: 'Book' }))

    expect(lastRequest(seen, 'POST', '/api/sessions').body).toMatchObject({ studentIds: ['student-1', 'student-2'] })
  })

  it('lists every conflict of a 409 and keeps what was typed', async () => {
    fakeFetch({
      'POST /api/sessions': {
        status: 409,
        body: {
          title: 'The booking breaks centre rules',
          conflicts: [
            conflict('room-overlap', 'R4 holds two sessions at once.'),
            conflict('tutor-overlap', 'T2 teaches two sessions at once.'),
            conflict('student-overlap', 'Vu Ha My is in two sessions at once.'),
          ],
        },
      },
    })
    const onBooked = renderForm()
    const user = userEvent.setup()

    await fillRowOne(user)
    await user.click(screen.getByRole('button', { name: 'Book' }))

    const lines = within(await screen.findByRole('list', { name: 'Conflicts' })).getAllByRole('listitem')
    expect(lines.map((line) => line.textContent)).toEqual([
      'Room overlapR4 holds two sessions at once.',
      'Tutor overlapT2 teaches two sessions at once.',
      'Student overlapVu Ha My is in two sessions at once.',
    ])
    expect(screen.getByLabelText('Room')).toHaveValue('R4')
    expect(screen.getByLabelText('Student')).toHaveValue('student-6')
    expect(onBooked).not.toHaveBeenCalled()
  })

  it("shows a 400's student message under the student field", async () => {
    fakeFetch({
      'POST /api/sessions': { status: 400, body: { errors: { studentIds: ['Unknown student: 42.'] } } },
    })
    renderForm()
    const user = userEvent.setup()

    await user.selectOptions(screen.getByLabelText('Student'), 'Vu Ha My')
    await user.click(screen.getByRole('button', { name: 'Book' }))

    expect(await screen.findByText('Unknown student: 42.')).toBeInTheDocument()
    expect(screen.getByLabelText('Student').parentElement).toHaveTextContent('Unknown student: 42.')
  })

  it('disables Book while the request runs', async () => {
    let answer: (value: FakeAnswer) => void = () => undefined
    const pending = new Promise<FakeAnswer>((resolve) => (answer = resolve))
    vi.stubGlobal('fetch', async () => {
      const { status, body } = await pending
      return new Response(JSON.stringify(body), { status })
    })
    renderForm()
    const user = userEvent.setup()

    await user.selectOptions(screen.getByLabelText('Student'), 'Vu Ha My')
    await user.click(screen.getByRole('button', { name: 'Book' }))
    expect(screen.getByRole('button', { name: 'Book' })).toBeDisabled()

    answer({ status: 201, body: aSession() })
    await vi.waitFor(() => expect(screen.getByRole('button', { name: 'Book' })).toBeEnabled())
  })

  it('says the API cannot be reached, and keeps what was typed', async () => {
    vi.stubGlobal('fetch', () => Promise.reject(new TypeError('Failed to fetch')))
    renderForm()
    const user = userEvent.setup()

    await user.selectOptions(screen.getByLabelText('Student'), 'Vu Ha My')
    await user.click(screen.getByRole('button', { name: 'Book' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Cannot reach the API.')
    expect(screen.getByLabelText('Student')).toHaveValue('student-6')
  })

  it('sends nothing without a student', async () => {
    const seen = fakeFetch({})
    renderForm()

    await userEvent.setup().click(screen.getByRole('button', { name: 'Book' }))

    expect(seen).toEqual([])
  })
})
