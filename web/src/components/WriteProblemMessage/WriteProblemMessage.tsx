import type { WriteProblem } from '../../useWrite'
import { ApiErrorMessage } from '../ApiErrorMessage/ApiErrorMessage'
import { ConflictList } from '../ConflictList/ConflictList'
import './WriteProblemMessage.css'

interface Props {
  problem: WriteProblem | null
  /** The fields the form shows a 400 message under. The others are listed here. */
  fields: string[]
}

/** Why the last write did not go through, above the form's buttons. */
export function WriteProblemMessage({ problem, fields }: Props) {
  if (!problem) {
    return null
  }
  switch (problem.kind) {
    case 'conflict':
      return <ConflictList conflicts={problem.conflicts} />
    case 'not-found':
      return (
        <p className="message message--error" role="alert">
          This session no longer exists.
        </p>
      )
    case 'error':
      return <ApiErrorMessage error={problem.error} />
    case 'invalid': {
      const others = Object.entries(problem.errors).filter(([field]) => !fields.includes(field))
      return others.length === 0 ? null : (
        <p className="message message--error" role="alert">
          {others.flatMap(([, messages]) => messages).join(' ')}
        </p>
      )
    }
  }
}
