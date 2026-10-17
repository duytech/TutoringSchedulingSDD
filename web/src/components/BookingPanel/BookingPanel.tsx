import { useEffect, type ReactNode } from 'react'
import './BookingPanel.css'

interface Props {
  title: string
  onClose: () => void
  children: ReactNode
}

/** The side panel beside the room grid that holds a booking form or a session. ✕ or Escape closes it. */
export function BookingPanel({ title, onClose, children }: Props) {
  useEffect(() => {
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        onClose()
      }
    }
    document.addEventListener('keydown', closeOnEscape)
    return () => {
      document.removeEventListener('keydown', closeOnEscape)
    }
  }, [onClose])

  return (
    <aside className="panel" aria-label={title}>
      <header className="panel__head">
        <h2>{title}</h2>
        <button type="button" className="panel__close" aria-label="Close" onClick={onClose}>
          ✕
        </button>
      </header>
      {children}
    </aside>
  )
}
