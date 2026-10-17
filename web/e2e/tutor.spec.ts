import { expect, test } from '@playwright/test'

// Row 11 of the phase 28 table: the tutor's page stays a read-only sheet.
test("the tutor's page has no booking button, and its cards open nothing", async ({ page }) => {
  await page.goto('/tutors/T1?date=2026-03-06')
  await expect(page.getByRole('heading', { name: /Ngoc Anh \(T1\)/ })).toBeVisible()

  await expect(page.getByRole('button', { name: '+ New session' })).toHaveCount(0)
  await page.getByRole('article').filter({ hasText: 'L021' }).click()
  await expect(page.getByRole('complementary')).toHaveCount(0)
})
