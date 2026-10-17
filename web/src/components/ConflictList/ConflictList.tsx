import type { ScheduleViolation } from '../../api'
import { ruleLabel } from '../../labels'
import './ConflictList.css'

/** Every rule a refused write breaks, as the API listed them. */
export function ConflictList({ conflicts }: { conflicts: ScheduleViolation[] }) {
  return (
    <ul className="conflicts" aria-label="Conflicts">
      {conflicts.map((conflict, index) => (
        <li key={`${conflict.rule}-${index}`} className="conflict">
          <span className="conflict__rule">{ruleLabel(conflict.rule)}</span>
          <span className="conflict__message">{conflict.message}</span>
        </li>
      ))}
    </ul>
  )
}
