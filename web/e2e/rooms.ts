import { expect, type Locator, type Page } from '@playwright/test'

// Steps the e2e tests share, written the way a receptionist uses the room grid.

/** Opens the room grid on a date and waits for its cards. */
export async function openDay(page: Page, date: string): Promise<void> {
  await page.goto(`/rooms?date=${date}`)
  await expect(page.getByRole('button', { name: '+ New session' })).toBeVisible()
}

/** The card of the seeded lesson with this id (each card lists its students' lesson ids). */
export function lessonCard(page: Page, lessonId: string): Locator {
  return page.getByRole('article').filter({ hasText: lessonId })
}

/** Opens a seeded lesson in the side panel. */
export async function openLesson(page: Page, lessonId: string): Promise<Locator> {
  await lessonCard(page, lessonId).click()
  const panel = page.getByRole('complementary', { name: 'Session' })
  await expect(panel).toBeVisible()
  return panel
}

export interface Booking {
  date: string
  time: string
  durationMin: 60 | 90
  roomId: string
  tutorId: string
  student: string
}

/** Fills in the New session form and presses Book. */
export async function book(page: Page, booking: Booking): Promise<Locator> {
  await page.getByRole('button', { name: '+ New session' }).click()
  const form = page.getByRole('form', { name: 'New session' })
  await form.getByLabel('Date').fill(booking.date)
  await form.getByLabel('Start').selectOption(booking.time)
  await form.getByLabel('Length').selectOption(String(booking.durationMin))
  await form.getByLabel('Room').selectOption(booking.roomId)
  await form.getByLabel('Tutor').selectOption(booking.tutorId)
  await form.getByLabel('Student', { exact: true }).selectOption({ label: booking.student })
  await form.getByRole('button', { name: 'Book' }).click()
  return form
}

/** The rule labels of the conflict list, in order. */
export function conflictRules(scope: Page | Locator): Locator {
  return scope.getByRole('list', { name: 'Conflicts' }).locator('.conflict__rule')
}
