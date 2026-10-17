import './RangeForm.css'

export interface Range {
  from: string | null
  to: string | null
}

export function RangeForm({ range, onChange }: { range: Range; onChange: (range: Range) => void }) {
  return (
    <form className="range" onSubmit={(event) => event.preventDefault()}>
      <label>
        From{' '}
        <input
          type="date"
          value={range.from ?? ''}
          onChange={(event) => onChange({ ...range, from: event.target.value || null })}
        />
      </label>
      <label>
        To{' '}
        <input
          type="date"
          value={range.to ?? ''}
          onChange={(event) => onChange({ ...range, to: event.target.value || null })}
        />
      </label>
      <button type="button" onClick={() => onChange({ from: null, to: null })} disabled={!range.from && !range.to}>
        Clear
      </button>
    </form>
  )
}
