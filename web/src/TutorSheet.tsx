import type { TutorDaySheet } from './api'
import { changeLine } from './labels'
import { SessionCard } from './SessionCard'

interface Props {
  sheet: TutorDaySheet
  tutorColour: string
  onGoToDate: (date: string) => void
}

/** One tutor's day as a list by time, with what changed after they were told at the top. */
export function TutorSheet({ sheet, tutorColour, onGoToDate }: Props) {
  return (
    <>
      {sheet.changesAfterCutoff.length > 0 && (
        <section className="changes" aria-label="Changed after you were told">
          <h2>Changed after you were told</h2>
          <ul>
            {sheet.changesAfterCutoff.map((change, index) => (
              <li key={`${change.sessionId}-${change.attendeeId ?? 'session'}-${change.kind}-${index}`}>{changeLine(change)}</li>
            ))}
          </ul>
        </section>
      )}

      {sheet.sessions.length === 0 ? (
        <p className="message">No lessons on this day.</p>
      ) : (
        <div className="sheet">
          {sheet.sessions.map((session) => (
            <SessionCard
              key={session.id}
              session={session}
              tutorColour={tutorColour}
              shownDate={sheet.date}
              onGoToDate={onGoToDate}
            />
          ))}
        </div>
      )}
    </>
  )
}
