import { useId, type ReactNode } from 'react'
import './Field.css'

interface Props {
  label: string
  /** The field's 400 message, if any. */
  error?: string | null
  /** The control, given the id its label points at. */
  children: (id: string) => ReactNode
}

/**
 * One form field: its label, the control, and the API's 400 message under it. The label points at the control by
 * id rather than wrapping it, so the control's name is the label alone, not the label plus its options.
 */
export function Field({ label, error = null, children }: Props) {
  const id = useId()
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      {children(id)}
      {error && <span className="field__error">{error}</span>}
    </div>
  )
}
