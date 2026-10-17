import type { ScheduleViolation } from '../../api'
import { longDate } from '../../dates'
import { ruleLabel } from '../../labels'
import { groupByDate } from '../../violations'
import './ViolationList.css'

export function ViolationList({ violations }: { violations: ScheduleViolation[] }) {
  return (
    <div className="violations">
      {groupByDate(violations).map((day) => (
        <section key={day.date}>
          <h2>{longDate(day.date)}</h2>
          <ul>
            {day.violations.map((violation) => (
              <li key={`${violation.rule}|${violation.message}`} className="violation">
                <span className="violation__rule">{ruleLabel(violation.rule)}</span>
                <div>
                  <p className="violation__message">{violation.message}</p>
                  {violation.lessonIds.length > 0 && (
                    <p className="violation__lessons">{violation.lessonIds.join(' · ')}</p>
                  )}
                </div>
              </li>
            ))}
          </ul>
        </section>
      ))}
    </div>
  )
}
