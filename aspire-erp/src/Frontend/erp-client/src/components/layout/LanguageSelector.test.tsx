import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { useTranslation } from 'react-i18next'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { LanguageSelector } from './LanguageSelector'
import { LANGUAGE_STORAGE_KEY } from '../../lib/i18n'
import { loadTestFixtures } from '../../test/i18n-fixtures'

// The production backend loads namespaces over HTTP, which does not exist under Vitest.
// This no-op backend answers `{}` for everything; real strings come from `loadTestFixtures`.
// It must be mocked here (hoisted above the singleton import) in every suite that renders
// translated components.
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

/** Visible witness: proves the UI itself re-renders, not just the select control. */
function Probe() {
  const { t } = useTranslation('common')
  return <p data-testid="probe">{t('header.language')}</p>
}

describe('LanguageSelector (spec 00-i18n F-20, automated F-10)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
  })

  it('loads English, re-renders in Spanish on select, and persists the choice', async () => {
    const user = userEvent.setup()
    render(
      <>
        <LanguageSelector />
        <Probe />
      </>,
    )

    // English first paint, detector cache included.
    expect(screen.getByTestId('probe')).toHaveTextContent('Language')
    expect(screen.getByTestId('language-select')).toHaveValue('en')

    // Switching re-renders translated UI…
    await user.selectOptions(screen.getByTestId('language-select'), 'es')
    await waitFor(() => expect(screen.getByTestId('probe')).toHaveTextContent('Idioma'))
    expect(screen.getByTestId('language-select')).toHaveValue('es')

    // …and the choice survives a reload: this is the key the detector reads on boot and
    // `apiClient` sends as `Accept-Language`.
    expect(localStorage.getItem(LANGUAGE_STORAGE_KEY)).toBe('es')
  })
})
