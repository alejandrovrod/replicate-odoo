import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// RTL auto-cleanup is wired to globals only when `globals: true`; this project uses explicit
// imports, so cleanup runs here. localStorage is cleared for the same reason: language
// persistence tests assert exact stored values and must not leak state across files.
afterEach(() => {
  cleanup()
  localStorage.clear()
})
