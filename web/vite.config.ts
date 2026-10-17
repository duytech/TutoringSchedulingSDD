/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API runs on its own (dotnet run --project src/BrightPath.Api). The dev server forwards /api to it,
// so the page and the API share one origin and no CORS setup is needed.
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': 'http://localhost:5238',
    },
  },
  // Unit and component tests only: the end-to-end tests in e2e/ run with Playwright (npm run e2e).
  // The environment stays Node; a component test opts in to jsdom on its first line.
  test: {
    include: ['src/**/*.test.{ts,tsx}'],
    setupFiles: ['src/testSetup.ts'],
  },
})
