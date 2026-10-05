import { describe, expect, it, vi } from 'vitest'
import { normalizeLanguage } from './i18n'

// The singleton boots on import (its HTTP backend is neutralized here); the function under
// test is pure, but the import side effect requires the mock regardless.
vi.mock('i18next-http-backend', () => ({
  default: class NoopBackend {
    static type = 'backend' as const
    init(): void {}
    read(
      _language: string,
      _namespace: string,
      callback: (err: null, data: Record<string, never>) => void,
    ): void {
      callback(null, {})
    }
  },
}))

describe('normalizeLanguage (reload-persistence contract)', () => {
  it.each([
    ['es', 'es'],
    ['es-AR', 'es'],
    ['es-ES', 'es'],
    ['en', 'en'],
    ['en-US', 'en'],
    ['en-GB', 'en'],
    ['fr-FR', 'en'],
    ['', 'en'],
  ])('maps %s to %s', (input, expected) => {
    expect(normalizeLanguage(input === '' ? null : input)).toBe(expected)
  })

  it('defaults null/undefined to English', () => {
    expect(normalizeLanguage(null)).toBe('en')
    expect(normalizeLanguage(undefined)).toBe('en')
  })
})
