import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { OpportunityDetailModal } from './OpportunityDetailModal'
import { apiClient } from '../../../api/client'
import { loadTestFixtures } from '../../../test/i18n-fixtures'

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
const put = () => apiClient.put as unknown as Mock

const DETAIL = {
  id: 'opp-1',
  companyId: 'c-1',
  opportunityNumber: 'OPP-2026-00042',
  opportunityFrom: 'Customer',
  partyId: 'cust-1',
  partyName: 'ACME Corp',
  stage: 'Negotiation',
  status: 'Open',
  opportunityAmount: 200,
  probability: 80,
  weightedAmount: 160,
  currencyId: null,
  currencyCode: 'USD',
  exchangeRate: 1,
  baseOpportunityAmount: 200,
  expectedClosingDate: '2026-12-31',
  territoryId: null,
  territoryName: null,
  industryId: null,
  industryName: null,
  marketSegment: null,
  competitors: ['Globex'],
  lossReasonId: null,
  lossReasonName: null,
  lossReason: null,
  assignedSalespersonId: null,
  rowVersion: 'AQIDBA==',
  items: [
    {
      id: 'li-1',
      itemId: 'it-1',
      itemName: 'Steel Bracket',
      quantity: 2,
      rate: 100,
      discountPercentage: 10,
      amount: 180,
    },
  ],
  activities: [
    {
      id: 'a-1',
      type: 'Note',
      subject: 'Converted',
      content: 'Lead converted',
      activityDate: '2026-10-01T00:00:00Z',
      nextFollowUpDate: null,
      createdByUserId: 'u-1',
    },
  ],
  quotations: [],
}

function mockCatalogs() {
  get().mockImplementation((url: string) => {
    if (url.startsWith('/v1/opportunities/')) return Promise.resolve({ data: DETAIL })
    if (url === '/v1/items') {
      return Promise.resolve({ data: { items: [{ id: 'it-1', code: 'IT-001', name: 'Steel Bracket' }] } })
    }
    if (url === '/v1/territories') {
      return Promise.resolve({ data: [{ id: 't-north', code: 'NORTH', name: 'North' }] })
    }
    if (url === '/v1/industries') {
      return Promise.resolve({ data: [{ id: 'i-ret', code: 'RETAIL', name: 'Retail' }] })
    }
    if (url === '/v1/currencies') return Promise.resolve({ data: [] })
    return Promise.reject(new Error(`unexpected GET ${url}`))
  })
}

describe('OpportunityDetailModal (CRM parity dossier)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    get().mockReset()
    put().mockReset()
    mockCatalogs()
  })

  it('loads the dossier and previews reactive line totals', async () => {
    render(<OpportunityDetailModal opportunityId="opp-1" onClose={() => {}} onSaved={() => {}} />)

    expect(await screen.findByText('OPP-2026-00042 — ACME Corp')).toBeInTheDocument()
    // 2 × 100 × 0.9 = 180 lines total, base converted at rate 1.
    expect(screen.getByText('180')).toBeInTheDocument()

    await userEvent.setup().click(screen.getByRole('tab', { name: 'Activities & History' }))
    expect(await screen.findByText('Converted')).toBeInTheDocument()
    expect(screen.getByText('Lead converted')).toBeInTheDocument()
  })

  it('saves the dossier carrying the read RowVersion', async () => {
    const user = userEvent.setup()
    const onSaved = vi.fn()
    put().mockResolvedValue({ data: { ...DETAIL, probability: 90 } })

    render(<OpportunityDetailModal opportunityId="opp-1" onClose={() => {}} onSaved={onSaved} />)
    await screen.findByText('OPP-2026-00042 — ACME Corp')

    await user.click(screen.getByRole('button', { name: 'Save dossier' }))

    await waitFor(() => expect(put()).toHaveBeenCalled())
    const [url, body] = put().mock.calls[0] as [string, Record<string, unknown>]
    expect(url).toBe('/v1/opportunities/opp-1')
    expect(body).toMatchObject({ opportunityId: 'opp-1', rowVersion: 'AQIDBA==' })
    expect(onSaved).toHaveBeenCalled()
  })

  it('locks saving on closed deals', async () => {
    get().mockImplementation((url: string) => {
      if (url.startsWith('/v1/opportunities/')) {
        return Promise.resolve({ data: { ...DETAIL, status: 'Won', stage: 'ClosedWon' } })
      }
      if (url === '/v1/items') return Promise.resolve({ data: { items: [] } })
      return Promise.resolve({ data: [] })
    })

    render(<OpportunityDetailModal opportunityId="opp-1" onClose={() => {}} onSaved={() => {}} />)
    await screen.findByText('OPP-2026-00042 — ACME Corp')

    expect(screen.getByRole('button', { name: 'Save dossier' })).toBeDisabled()
    expect(
      screen.getByText('Closed deals are read-only here; advance or re-open owns terminals.'),
    ).toBeInTheDocument()
  })
})
