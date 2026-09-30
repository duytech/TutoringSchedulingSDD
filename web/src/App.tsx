import { useState, type MouseEvent } from 'react'
import { DayPage } from './DayPage'
import { placeFromUrl, urlOf, type Link, type Place } from './place'
import { TutorDayPage } from './TutorDayPage'

/** Keeps the place in sync with the URL and shows its page: a tutor's day, or without a tutor the room grid. */
export default function App() {
  const [place, setPlace] = useState<Place>(placeFromUrl)

  // Keeps the place in the URL, so a reload stays on it.
  const goTo = (next: Place) => {
    if (next.date === place.date && next.tutor === place.tutor) {
      return
    }
    window.history.replaceState(null, '', urlOf(next))
    setPlace(next)
  }
  const go = (date: string | null) => goTo({ ...place, date })

  const linkTo = (next: Place): Link => ({
    href: urlOf(next),
    onClick: (event: MouseEvent) => {
      if (!event.metaKey && !event.ctrlKey && !event.shiftKey && event.button === 0) {
        event.preventDefault()
        goTo(next)
      }
    },
  })

  return place.tutor ? (
    <TutorDayPage tutor={place.tutor} date={place.date} onGoToDate={go} linkTo={linkTo} />
  ) : (
    <DayPage date={place.date} onGoToDate={go} linkTo={linkTo} />
  )
}
