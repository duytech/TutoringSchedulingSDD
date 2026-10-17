// @vitest-environment jsdom
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { BookingPanel } from './BookingPanel'

describe('BookingPanel', () => {
  it('closes with ✕', async () => {
    const onClose = vi.fn()
    render(<BookingPanel title="Session" onClose={onClose}>body</BookingPanel>)

    await userEvent.setup().click(screen.getByRole('button', { name: 'Close' }))

    expect(onClose).toHaveBeenCalledOnce()
  })

  it('closes with Escape', async () => {
    const onClose = vi.fn()
    render(<BookingPanel title="Session" onClose={onClose}>body</BookingPanel>)

    await userEvent.setup().keyboard('{Escape}')

    expect(onClose).toHaveBeenCalledOnce()
  })

  it('is named by its title', () => {
    render(<BookingPanel title="New session" onClose={vi.fn()}>body</BookingPanel>)

    expect(screen.getByRole('complementary', { name: 'New session' })).toHaveTextContent('body')
  })
})
