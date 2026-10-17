// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { aSession, NOW } from '../../testData'
import { SessionCard } from './SessionCard'

/** A session moved to another day, so its card has the "moved →" link. */
const moved = aSession({
  cancelledAt: NOW,
  movedTo: { id: 'new', startsAt: '2026-03-07T14:00:00+07:00', roomId: 'R3' },
})

function renderCard(props: { onSelect?: () => void; selected?: boolean; onGoToDate?: () => void } = {}) {
  render(
    <SessionCard
      session={moved}
      tutorColour="#123456"
      shownDate="2026-03-06"
      now={NOW}
      onGoToDate={props.onGoToDate ?? vi.fn()}
      onSelect={props.onSelect}
      selected={props.selected}
    />,
  )
  return screen.getByRole('article')
}

describe('SessionCard', () => {
  it('without onSelect (the tutor page), has no open button and does nothing on click', async () => {
    const card = renderCard()

    await userEvent.setup().click(card)

    expect(screen.queryByRole('button', { name: '10:30–12:00' })).not.toBeInTheDocument()
    expect(card).not.toHaveClass('card--selectable')
  })

  it('opens the session from its time button and from a click on the card', async () => {
    const onSelect = vi.fn()
    const card = renderCard({ onSelect })
    const user = userEvent.setup()

    await user.click(screen.getByRole('button', { name: '10:30–12:00' }))
    await user.click(card)

    expect(onSelect).toHaveBeenCalledTimes(2)
    expect(onSelect).toHaveBeenCalledWith(moved)
  })

  it('opens the session from the keyboard', async () => {
    const onSelect = vi.fn()
    renderCard({ onSelect })
    const user = userEvent.setup()

    await user.tab()
    await user.keyboard('{Enter}')

    expect(onSelect).toHaveBeenCalledOnce()
  })

  it('goes to the other day from the "moved →" link without opening the card', async () => {
    const onSelect = vi.fn()
    const onGoToDate = vi.fn()
    renderCard({ onSelect, onGoToDate })

    await userEvent.setup().click(screen.getByRole('button', { name: /moved →/ }))

    expect(onGoToDate).toHaveBeenCalledWith('2026-03-07')
    expect(onSelect).not.toHaveBeenCalled()
  })

  it('marks the selected card', () => {
    expect(renderCard({ onSelect: vi.fn(), selected: true })).toHaveClass('card--selected')
  })
})
