import type { CSSProperties } from 'react'
import type { ScheduleDay } from './api'
import { localDate } from './dates'
import { layoutDay, minutesOf, OPENING, rowCount } from './layout'
import { SessionCard } from './SessionCard'

interface Props {
  day: ScheduleDay
  /** The grid's columns, in order (GET /api/rooms). */
  roomIds: string[]
  tutorColour: (tutorId: string) => string
  onGoToDate: (date: string) => void
}

export function DayGrid({ day, roomIds, tutorColour, onGoToDate }: Props) {
  const rows = rowCount(OPENING)
  const opens = minutesOf(`T${OPENING.opens}`)
  const placements = layoutDay(day.sessions, roomIds)

  const labels = Array.from({ length: rows / 2 }, (_, index) => opens + index * 30)
  const nowRow = localDate(day.now) === day.date ? (minutesOf(day.now) - opens) / OPENING.stepMin : null
  const showNow = nowRow !== null && nowRow >= 0 && nowRow <= rows

  const gridStyle = { '--rooms': roomIds.length, '--rows': rows } as CSSProperties

  return (
    <div className="grid" style={gridStyle}>
      <div className="grid__corner" />
      {roomIds.map((id, index) => (
        <div key={id} className="grid__room" style={{ gridColumn: index + 2 }}>
          {id}
        </div>
      ))}

      {labels.map((minutes, index) => (
        <div
          key={minutes}
          className={`grid__line ${minutes % 60 === 0 ? 'grid__line--hour' : ''}`}
          style={{ gridRow: index * 2 + 2 }}
        >
          <span className="grid__time">
            {String(Math.floor(minutes / 60)).padStart(2, '0')}:{String(minutes % 60).padStart(2, '0')}
          </span>
        </div>
      ))}

      {day.sessions.map((session) => (
        <SessionCard
          key={session.id}
          session={session}
          placement={placements.get(session.id)}
          tutorColour={tutorColour(session.tutorId)}
          shownDate={day.date}
          onGoToDate={onGoToDate}
        />
      ))}

      {showNow && (
        <div
          className="grid__now"
          style={{ gridRow: Math.floor(nowRow) + 2, top: `${(nowRow % 1) * 100}%` }}
          title={`Now: ${day.now.slice(11, 16)}`}
        />
      )}
    </div>
  )
}
