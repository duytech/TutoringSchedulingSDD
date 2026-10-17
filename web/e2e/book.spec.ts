import { expect, test } from '@playwright/test'
import { book, conflictRules, openDay, type Booking } from './rooms.ts'

// Rows 1 and 2 of the phase 28 table (phase 11 rows 1 and 4).
const rowOne: Booking = { date: '2026-03-07', time: '13:00', durationMin: 60, roomId: 'R4', tutorId: 'T2', student: 'Vu Ha My' }

test('a booking lands on its day, and the same booking again is refused with every overlap', async ({ page }) => {
  await openDay(page, '2026-03-06')

  await book(page, rowOne)

  await expect(page).toHaveURL(/\?date=2026-03-07$/)
  await expect(page.getByRole('heading', { name: 'Saturday 7 March 2026' })).toBeVisible()
  await expect(page.getByRole('article', { name: 'Pham Duc in R4' })).toBeVisible()
  const panel = page.getByRole('complementary', { name: 'Session' })
  await expect(panel).toContainText('13:00–14:00 · T2 Pham Duc · R4')
  await expect(panel).toContainText('Vu Ha My')

  const form = await book(page, rowOne)

  await expect(conflictRules(form)).toHaveText(['Room overlap', 'Tutor overlap', 'Student overlap'])
  await expect(form.getByLabel('Room')).toHaveValue('R4')
})
