const TUTOR_COLOURS = ['#2f6fdb', '#d9822b', '#2a9d6f', '#9b51e0', '#c2410c', '#0e7490']

/** A tutor keeps one colour on the grid and on their own sheet: T1 is the first colour, T2 the second, and so on. */
export function tutorColour(tutorId: string): string {
  const tutorNumber = Number(tutorId.replace(/\D/g, '')) || 1
  return TUTOR_COLOURS[(tutorNumber - 1) % TUTOR_COLOURS.length]
}
