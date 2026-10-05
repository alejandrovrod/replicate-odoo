import { beforeEach, describe, expect, it, vi } from 'vitest'
import { translateErrorCode } from './translateErrorCode'
import { i18n, loadTestFixtures } from '../test/i18n-fixtures'

// Same no-op backend rationale as LanguageSelector.test.tsx: `translateErrorCode` asks the
// singleton, so the HTTP backend must be neutralized before it loads.
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

describe('translateErrorCode (spec 00-i18n F-21/F-22)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    await i18n.changeLanguage('es')
  })

  it('returns the Spanish catalog message for a known code (F-21)', () => {
    expect(translateErrorCode(i18n, 'crm_company_required', 'A company is required.')).toBe(
      'Se requiere una empresa.',
    )
  })

  it('falls back to the backend detail for an unknown code (F-22)', () => {
    expect(translateErrorCode(i18n, 'nope_missing_code', 'Detalle del dominio.')).toBe(
      'Detalle del dominio.',
    )
  })

  it('falls back when there is no code at all (F-22)', () => {
    expect(translateErrorCode(i18n, null, 'Fallback')).toBe('Fallback')
    expect(translateErrorCode(i18n, undefined, null)).toBe('')
  })
})
