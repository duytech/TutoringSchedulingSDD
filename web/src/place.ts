import type { MouseEvent } from 'react'

/** What the page shows: a date (none means the API's today), and a tutor's sheet or, without one, the room grid. */
export interface Place {
  date: string | null
  tutor: string | null
}

/** A real link (it opens in a new tab too) that a plain click follows without a reload. */
export interface Link {
  href: string
  onClick: (event: MouseEvent) => void
}

export function placeFromUrl(): Place {
  const params = new URLSearchParams(window.location.search)
  return { date: params.get('date'), tutor: params.get('tutor') }
}

export function urlOf({ date, tutor }: Place): string {
  const url = new URL(window.location.href)
  url.search = ''
  if (tutor) {
    url.searchParams.set('tutor', tutor)
  }
  if (date) {
    url.searchParams.set('date', date)
  }
  return url.toString()
}
