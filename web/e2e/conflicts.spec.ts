import { expect, test } from '@playwright/test'
import { book, conflictRules, openDay } from './rooms.ts'

// Row 3 of the phase 28 table (phase 11 row 6): T1 already has 7 sessions on 03-06.
test("an eighth session for T1 is refused with only the tutor load, naming the day's sessions", async ({ page }) => {
  await openDay(page, '2026-03-06')

  const form = await book(page, {
    date: '2026-03-06',
    time: '14:30',
    durationMin: 60,
    roomId: 'R4',
    tutorId: 'T1',
    student: 'Do Van Kien',
  })

  await expect(conflictRules(form)).toHaveText(['Tutor load'])
  await expect(form.getByRole('list', { name: 'Conflicts' })).toContainText('T1 Ngoc Anh has 8 sessions on 2026-03-06; the limit is 6.')
})
