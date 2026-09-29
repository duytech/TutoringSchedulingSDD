// Calendar dates as "YYYY-MM-DD" strings, done in UTC so the browser's zone never shifts a day.

export function addDays(date: string, days: number): string {
  const d = new Date(`${date}T00:00:00Z`)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

/** "2026-03-06" → "Friday 6 March 2026". */
export function longDate(date: string): string {
  const d = new Date(`${date}T00:00:00Z`)
  const part = (options: Intl.DateTimeFormatOptions) =>
    new Intl.DateTimeFormat('en-GB', { ...options, timeZone: 'UTC' }).format(d)
  return `${part({ weekday: 'long' })} ${d.getUTCDate()} ${part({ month: 'long' })} ${d.getUTCFullYear()}`
}

/** "2026-03-06T10:30:00+07:00" → "10:30", the centre's local time as sent. */
export function localTime(localIso: string): string {
  return localIso.slice(11, 16)
}

/** "2026-03-06T10:30:00+07:00" → "2026-03-06". */
export function localDate(localIso: string): string {
  return localIso.slice(0, 10)
}
