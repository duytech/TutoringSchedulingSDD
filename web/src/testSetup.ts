// Loaded before every test file (vite.config.ts). The jest-dom matchers (toBeDisabled, toHaveTextContent, …) and
// an unmount after each test, which Testing Library only does by itself when the test globals are on.
import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

afterEach(() => {
  cleanup()
})
