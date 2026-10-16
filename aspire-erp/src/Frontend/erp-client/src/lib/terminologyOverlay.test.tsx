import { render, screen, waitFor } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { useTranslation } from 'react-i18next'
import { TerminologyOverlayProvider, useTerminology } from './terminologyOverlay'
import { apiClient } from '../api/client'
import { i18n, loadTestFixtures } from '../test/i18n-fixtures'

// Same no-op backend rationale as PeriodClosingList.test.tsx: components ask the singleton.
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

vi.mock('../api/client', () => ({
  ApiError: class MockApiError extends Error {
    status: number
    code?: string
    constructor(status: number, title: string, detail: string, code?: string) {
      super(detail || title)
      this.status = status
      this.code = code
    }
  },
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}))

const get = () => apiClient.get as unknown as Mock

function Probe() {
  const { t } = useTranslation('selling')
  const { presetKey, ready } = useTerminology()
  if (!ready) return <p>loading</p>
  return (
    <p>
      {t('customerForm.titleNew', 'New Customer')} · {presetKey ?? 'none'}
    </p>
  )
}

describe('TerminologyOverlayProvider (workflow Fase 5, gap D4)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    await i18n.changeLanguage('en')
    get().mockReset()
  })

  it('overrides catalog terms with the active preset vocabulary (WF-PRESET-G4)', async () => {
    get().mockResolvedValue({
      data: {
        presetKey: 'healthcare',
        name: 'Healthcare',
        terminology: {
          en: { selling: { 'customerForm.titleNew': 'New Patient' } },
          es: { selling: { 'customerForm.titleNew': 'Nuevo Paciente' } },
        },
      },
    })

    render(
      <TerminologyOverlayProvider>
        <Probe />
      </TerminologyOverlayProvider>,
    )

    // Wire contract untouched (no DTO/table change), screen vocabulary replaced.
    expect(await screen.findByText('New Patient · healthcare')).toBeInTheDocument()
    expect(get()).toHaveBeenCalledWith('/v1/presets/active')
  })

  it('renders the shipped catalog when no preset is active', async () => {
    get().mockResolvedValue({ presetKey: null, name: null, terminology: {} })

    render(
      <TerminologyOverlayProvider>
        <Probe />
      </TerminologyOverlayProvider>,
    )

    await waitFor(() => expect(screen.getByText(/none/)).toBeInTheDocument())
  })
})
