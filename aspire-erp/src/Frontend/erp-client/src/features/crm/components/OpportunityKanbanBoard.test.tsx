import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { OpportunityKanbanBoard } from './OpportunityKanbanBoard'
import { apiClient } from '../../../api/client'
import { i18n, loadTestFixtures } from '../../../test/i18n-fixtures'

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

vi.mock('../../../api/client', () => ({
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

const OPEN_DEAL = {
  id: 'opp-1',
  companyId: 'c-1',
  opportunityNumber: 'OPP-2026-00042',
  partyName: 'ACME Corp',
  stage: 'Negotiation',
  status: 'Open',
  opportunityAmount: 50000,
  probability: 80,
  weightedAmount: 40000,
  currencyId: null,
  lossReason: null,
  rowVersion: 'AQIDBA==',
}

const WON_DEAL = {
  ...OPEN_DEAL,
  id: 'opp-won',
  opportunityNumber: 'OPP-2026-00043',
  stage: 'ClosedWon',
  status: 'Won',
  probability: 100,
}

function mockBoard(deals = [OPEN_DEAL, WON_DEAL]) {
  get().mockImplementation((url: string) => {
    if (url === '/v1/opportunities') {
      return Promise.resolve({ data: { items: deals, totalCount: deals.length } })
    }
    if (url === '/v1/loss-reasons') {
      return Promise.resolve({ data: [{ id: 'r-comp', code: 'COMPETITOR', name: 'Lost to Competitor' }] })
    }
    return Promise.reject(new Error(`unexpected GET ${url}`))
  })
}

function dropOnStage(cardNumber: string, stage: string) {
  const card = screen.getByText(cardNumber).closest('div[draggable="true"]')!
  fireEvent.dragStart(card, {
    dataTransfer: { setData: vi.fn(), getData: () => cardNumber === 'OPP-2026-00042' ? 'opp-1' : 'opp-won' },
  })
  const column = screen.getByText(stage).closest('div')!.parentElement!
  fireEvent.drop(column, { dataTransfer: { getData: () => (cardNumber === 'OPP-2026-00042' ? 'opp-1' : 'opp-won') } })
}

describe('OpportunityKanbanBoard (CRM parity pipeline)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    // The board renders catalog-backed labels without defaults (salesOrder.create);
    // register the minimal bundle so assertions read UI text, not raw keys.
    i18n.addResourceBundle('en', 'crm', { salesOrder: { create: 'Create Sales Order' } }, true, true)
    await i18n.changeLanguage('en')
    get().mockReset()
    post().mockReset()
    mockBoard()
  })

  it('renders cards and advances on drop with the read RowVersion', async () => {
    post().mockResolvedValue({ data: { ...OPEN_DEAL, stage: 'Proposal', probability: 50 } })

    render(<OpportunityKanbanBoard />)
    await screen.findByText('OPP-2026-00042')

    dropOnStage('OPP-2026-00042', 'Proposal')

    await waitFor(() => expect(post()).toHaveBeenCalled())
    const [url, body, config] = post().mock.calls[0] as [string, Record<string, unknown>, Record<string, unknown>]
    expect(url).toBe('/v1/opportunities/opp-1/advance')
    expect(body).toMatchObject({ toStage: 'Proposal' })
    expect(config.params).toMatchObject({ rowVersion: 'AQIDBA==' })
  })

  it('opens the loss dialog on ClosedLost drop and advances with the catalog reason', async () => {
    const user = userEvent.setup()
    post().mockResolvedValue({ data: { ...OPEN_DEAL, stage: 'ClosedLost', status: 'Lost' } })

    render(<OpportunityKanbanBoard />)
    await screen.findByText('OPP-2026-00042')

    dropOnStage('OPP-2026-00042', 'ClosedLost')

    // Accessible dialog (not window.prompt) with the catalog reasons.
    expect(await screen.findByRole('heading', { name: 'Close as Lost' })).toBeInTheDocument()
    await user.selectOptions(screen.getByLabelText('Loss Reason'), 'r-comp')
    await user.click(screen.getByRole('button', { name: 'Close as Lost' }))

    await waitFor(() => expect(post()).toHaveBeenCalled())
    const [, body] = post().mock.calls[0] as [string, Record<string, unknown>]
    expect(body).toMatchObject({ toStage: 'ClosedLost', lossReasonId: 'r-comp' })
  })

  it('offers Create Quotation as the primary Won action next to the sales-order shortcut', async () => {
    render(<OpportunityKanbanBoard />)
    await screen.findByText('OPP-2026-00043')

    expect(screen.getByRole('button', { name: 'Create Quotation' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Create Sales Order' })).toBeInTheDocument()
  })
})
