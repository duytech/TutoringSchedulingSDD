import type { ChangedBy } from '../../api'
import { Field } from '../Field/Field'
import './ChangedBySelect.css'

const CHOICES: ChangedBy[] = ['family', 'tutor', 'centre']

interface Props {
  label: string
  /** Empty until someone is picked: there is no default, because it decides who pays. */
  value: ChangedBy | ''
  onChange: (value: ChangedBy) => void
  error: string | null
}

/** Who asked for a cancel or a move: family, tutor or centre. Required. */
export function ChangedBySelect({ label, value, onChange, error }: Props) {
  return (
    <Field label={label} error={error}>
      {(id) => (
        <select id={id} required value={value} onChange={(event) => onChange(event.target.value as ChangedBy)}>
          <option value="">Choose who asked</option>
          {CHOICES.map((choice) => (
            <option key={choice}>{choice}</option>
          ))}
        </select>
      )}
    </Field>
  )
}
