import { useState } from 'react'
import { asApiError, type ApiError, type Session, type WriteResult } from './api'

/** Why the last write did not go through: the API's refusal, or an API that could not be reached. */
export type WriteProblem = Exclude<WriteResult, { kind: 'ok' }> | { kind: 'error'; error: ApiError }

/**
 * Runs one write at a time for a form: `sending` while it runs, then either `onDone` with the session the API sent
 * back, or the `problem` to show. The form keeps its own fields, so what was typed stays after a refusal.
 */
export function useWrite(onDone: (session: Session) => void) {
  const [sending, setSending] = useState(false)
  const [problem, setProblem] = useState<WriteProblem | null>(null)

  async function runWrite(write: () => Promise<WriteResult>) {
    setSending(true)
    setProblem(null)
    try {
      const result = await write()
      if (result.kind === 'ok') {
        onDone(result.session)
      } else {
        setProblem(result)
      }
    } catch (cause: unknown) {
      setProblem({ kind: 'error', error: asApiError(cause) })
    } finally {
      setSending(false)
    }
  }

  return { sending, problem, runWrite }
}

/** The 400 message for one field, if there is one. */
export function fieldError(problem: WriteProblem | null, field: string): string | null {
  if (problem?.kind !== 'invalid' || !(field in problem.errors)) {
    return null
  }
  return problem.errors[field].join(' ')
}
