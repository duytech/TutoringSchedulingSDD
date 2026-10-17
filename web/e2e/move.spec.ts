import { expect, test } from '@playwright/test'
import { conflictRules, lessonCard, openDay, openLesson } from './rooms.ts'

// Rows 6 and 7 of the phase 28 table (phase 18 rows 1 and 6).
test('moving L028 to 14:00 opens the new session and labels the old card', async ({ page }) => {
  await openDay(page, '2026-03-07')
  const panel = await openLesson(page, 'L028')

  const move = panel.getByRole('form', { name: 'Move' })
  await move.getByLabel('Start').selectOption('14:00')
  await move.getByLabel('Moved by').selectOption('family')
  await move.getByRole('button', { name: 'Move' }).click()

  await expect(panel).toContainText('14:00–15:30 · T3 Le Thu · R3')
  await expect(lessonCard(page, 'L028')).toContainText('moved → 14:00 R3')
  await expect(lessonCard(page, 'L028')).toHaveClass(/card--cancelled/)
})

test('moving L021 within its over-loaded day is refused with only the tutor load', async ({ page }) => {
  await openDay(page, '2026-03-06')
  const panel = await openLesson(page, 'L021')

  const move = panel.getByRole('form', { name: 'Move' })
  await move.getByLabel('Start').selectOption('14:30')
  await move.getByLabel('Room').selectOption('R4')
  await move.getByLabel('Moved by').selectOption('tutor')
  await move.getByRole('button', { name: 'Move' }).click()

  await expect(conflictRules(move)).toHaveText(['Tutor load'])
  await expect(move.getByRole('list', { name: 'Conflicts' })).toContainText('has 7 sessions')
})
