import { expect, test } from '@playwright/test'
import { lessonCard, openDay, openLesson } from './rooms.ts'

interface DayAnswer {
  sessions: { attendees: { lessonId: string | null; status: string; chargeable: boolean }[] }[]
}

// Rows 4 and 5 of the phase 28 table (phase 13 rows 1 and 7).
test('a late family cancel of L020 is chargeable, after the cut-off, and cancels the session', async ({ page }) => {
  await openDay(page, '2026-03-06')
  const panel = await openLesson(page, 'L020')

  await panel.getByRole('button', { name: 'Cancel Bui An Nhien' }).click()
  await panel.getByLabel('Cancelled by').selectOption('family')
  await panel.getByRole('button', { name: 'Confirm cancel' }).click()

  await expect(panel.getByRole('status')).toHaveText(
    'Bui An Nhien cancelled · chargeable · after the tutor was told · the session is cancelled',
  )
  await expect(lessonCard(page, 'L020')).toHaveClass(/card--cancelled/)
  await expect(lessonCard(page, 'L020')).toContainText('changed after tutor was told')

  const day = (await (await page.request.get('/api/sessions?date=2026-03-06')).json()) as DayAnswer
  const attendee = day.sessions.flatMap((session) => session.attendees).find((one) => one.lessonId === 'L020')
  expect(attendee).toMatchObject({ status: 'cancelled', chargeable: true })
})

test('a session that has started (L018) offers no cancel and no move', async ({ page }) => {
  await openDay(page, '2026-03-06')
  const panel = await openLesson(page, 'L018')

  await expect(panel).toContainText('Started at 09:00 — it can no longer be moved or cancelled.')
  await expect(panel.getByRole('button', { name: /^Cancel / })).toHaveCount(0)
  await expect(panel.getByRole('form', { name: 'Move' })).toHaveCount(0)
})
