// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ConflictList } from './ConflictList'

const conflict = (rule: string, message: string) => ({ rule, date: '2026-03-06', sessionIds: [], lessonIds: [], message })

describe('ConflictList', () => {
  it('shows one line per conflict, with its rule label and message', () => {
    render(<ConflictList conflicts={[conflict('closed-day', 'Closed on Mondays.'), conflict('in-the-past', 'Too late.')]} />)

    expect(screen.getAllByRole('listitem').map((line) => line.textContent)).toEqual([
      'Closed dayClosed on Mondays.',
      'In the pastToo late.',
    ])
  })

  it('shows a rule the web does not know as its code', () => {
    render(<ConflictList conflicts={[conflict('new-rule', 'Something new.')]} />)

    expect(screen.getByText('new-rule')).toBeInTheDocument()
  })
})
