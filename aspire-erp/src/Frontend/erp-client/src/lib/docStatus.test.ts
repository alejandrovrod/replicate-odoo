import { beforeEach, describe, expect, it, vi } from 'vitest'
import { translateDocStatus } from './docStatus'
import { i18n, loadTestFixtures } from '../test/i18n-fixtures'

// Same no-op backend rationale as translateErrorCode.test.ts: the helper asks the
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

describe('translateDocStatus (selling read surfaces)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle(
      'es',
      'common',
      {
        docStatus: {
          Draft: 'Borrador',
          Paid: 'Pagada',
        },
      },
      true,
      true,
    )
    await i18n.changeLanguage('es')
  })

  it('returns the Spanish catalog label for a known status', () => {
    expect(translateDocStatus(i18n, 'Draft')).toBe('Borrador')
    expect(translateDocStatus(i18n, 'Paid')).toBe('Pagada')
  })

  it('falls back to the raw status for an unknown code', () => {
    expect(translateDocStatus(i18n, 'SomethingNew')).toBe('SomethingNew')
  })

  it('returns empty string when there is no status at all', () => {
    expect(translateDocStatus(i18n, null)).toBe('')
    expect(translateDocStatus(i18n, undefined)).toBe('')
  })
})
