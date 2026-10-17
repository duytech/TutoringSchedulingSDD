import { defineConfig, devices } from '@playwright/test'

// End-to-end tests: the web and the real API on a freshly seeded week (the clock pinned at 2026-03-06 10:00).
// The tests book, move and cancel, so they need a fresh stack each run:
//   docker compose down && docker compose up -d --build && scripts/smoke-test.sh && npm run e2e
// There is no webServer on purpose: Playwright would reuse a stack whose data an earlier run already changed.
export default defineConfig({
  testDir: 'e2e',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  forbidOnly: !!process.env.CI,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
    trace: 'retain-on-failure',
  },
  projects: [{ name: 'chromium', use: { ...devices['Desktop Chrome'] } }],
})
