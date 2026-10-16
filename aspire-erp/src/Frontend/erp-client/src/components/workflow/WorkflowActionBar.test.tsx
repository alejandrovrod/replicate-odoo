import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { WorkflowActionBar } from './WorkflowActionBar'
import { apiClient } from '../../api/client'
import { i18n, loadTestFixtures } from '../../test/i18n-fixtures'

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

vi.mock('../../api/client', () => ({
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
const post = () => apiClient.post as unknown as Mock

const STATUS = {
  documentId: 'order-1',
  documentType: 'SalesOrder',
  currentState: 'Admision',
  transitions: [
    { toState: 'Consulta', actionLabel: 'Iniciar consulta', allowedRole: 'Recepcion', allowedForCaller: true },
    { toState: 'Autorizado', actionLabel: 'Autorizar', allowedRole: 'Medico', allowedForCaller: false },
  ],
}

describe('WorkflowActionBar (workflow Fase 5, gap D5)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle('en', 'common', { workflow: {} }, true, true)
    await i18n.changeLanguage('en')
    get().mockReset()
    post().mockReset()
  })

  it('shows the state badge and only caller-allowed moves', async () => {
    const user = userEvent.setup()
    get().mockResolvedValue({ data: STATUS })
    post().mockResolvedValue({ data: { ...STATUS, currentState: 'Consulta', transitions: [] } })
    const refreshed: string[] = []

    render(
      <WorkflowActionBar
        documentType="SalesOrder"
        companyId="c-1"
        documentId="order-1"
        onTransition={() => refreshed.push('ok')}
      />,
    )

    expect(await screen.findByText('Admision')).toBeInTheDocument()
    expect(get()).toHaveBeenCalledWith('/v1/workflows/status', {
      params: { documentType: 'SalesOrder', companyId: 'c-1', documentId: 'order-1' },
    })

    // Only the allowed move renders as a button; the forbidden one stays hidden.
    expect(screen.getByRole('button', { name: 'Iniciar consulta' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Autorizar' })).toBeNull()

    await user.click(screen.getByRole('button', { name: 'Iniciar consulta' }))
    await waitFor(() => expect(post()).toHaveBeenCalledTimes(1))
    expect(post()).toHaveBeenCalledWith('/v1/workflows/transition', {
      documentType: 'SalesOrder',
      companyId: 'c-1',
      documentId: 'order-1',
      toState: 'Consulta',
    })
    expect(refreshed).toEqual(['ok'])
  })

  it('hides itself when no flow governs the document', async () => {
    get().mockRejectedValue(new Error('Not found'))
    const { container } = render(
      <WorkflowActionBar documentType="SalesOrder" companyId="c-1" documentId="order-9" />,
    )

    await waitFor(() => expect(get()).toHaveBeenCalled())
    expect(container).toBeEmptyDOMElement()
  })
})
