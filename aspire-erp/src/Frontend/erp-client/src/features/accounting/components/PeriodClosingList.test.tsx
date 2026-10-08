import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { PeriodClosingList } from './PeriodClosingList'
import { apiClient, ApiError } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'
import { i18n, loadTestFixtures } from '../../../test/i18n-fixtures'

// Same no-op backend rationale as LanguageSelector.test.tsx: components ask the singleton.
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

vi.mock('../../../api/client', () => {
  class MockApiError extends Error {
    status: number
    code?: string
    title?: string
    constructor(status: number, title: string, detail: string, code?: string) {
      super(detail || title)
      this.name = 'ApiError'
      this.status = status
      this.title = title
      this.code = code
    }
  }
  return {
    ApiError: MockApiError,
    apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
  }
})

const get = () => apiClient.get as unknown as Mock
const post = () => apiClient.post as unknown as Mock

const OPEN_YEAR = {
  id: 'y-open',
  companyId: 'c-1',
  yearName: 'FY-2025',
  startDate: '2025-01-01',
  endDate: '2025-12-31',
  isClosed: false,
  closedAt: null,
  rowVersion: 'AAA=',
}

const DRAFT_VOUCHER = {
  id: 'v-draft',
  companyId: 'c-1',
  fiscalYearId: 'y-open',
  voucherNo: 'DRAFT',
  postingDate: '2025-12-31',
  retainedEarningsAccountId: 'r-1',
  documentStatus: 'Draft',
  remarks: null,
  rowVersion: 'AAA=',
}

const SUBMITTED_VOUCHER = {
  ...DRAFT_VOUCHER,
  id: 'v-sub',
  voucherNo: 'PCV-2025-1',
  documentStatus: 'Submitted',
}

const pageOf = (items: unknown[]) => ({
  data: { items, totalCount: items.length, pageNumber: 1, pageSize: 50, totalPages: 1 },
})

const PREVIEW = {
  data: {
    companyId: 'c-1',
    fiscalYearId: 'y-open',
    lines: [
      {
        accountId: 'a-4000',
        code: '4000',
        name: 'Revenue',
        rootType: 'Income',
        debit: 500000,
        credit: 0,
        balance: -500000,
      },
    ],
    retainedLine: {
      accountId: 'r-1',
      code: '3100',
      name: 'Retained Earnings',
      rootType: 'Equity',
      debit: 0,
      credit: 120000,
      balance: 120000,
    },
    net: 120000,
  },
}

describe('PeriodClosingList (R-13 Task 5.2)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle(
      'en',
      'accounting',
      {
        fiscalYear: { statusClosed: 'Closed' },
        fiscalYearForm: { cancel: 'Cancel' },
        periodClosing: {
          title: 'Period Closing Vouchers',
          newBtn: 'New Closing Voucher',
          filterYearAll: 'All fiscal years',
          filterStatusAll: 'All statuses',
          statusDraft: 'Draft',
          statusSubmitted: 'Submitted',
          statusCancelled: 'Cancelled',
          submitBtn: 'Submit',
          cancelBtn: 'Cancel',
          previewBtn: 'Preview',
          hidePreview: 'Hide preview',
          previewTitle: 'P&L preview',
          previewNetProfit: 'Net profit {{amount}}',
        },
      },
      true,
      true,
    )
    i18n.addResourceBundle(
      'en',
      'error',
      { duplicate_closing_for_fiscal_year: 'A submitted close already exists.' },
      true,
      true,
    )
    await i18n.changeLanguage('en')
    useTenantStore.setState({ companyId: 'c-1' })
    get().mockReset()
    post().mockReset()
    get().mockImplementation((url: string) => {
      if (typeof url === 'string' && url.includes('unclosed-balances')) return Promise.resolve(PREVIEW)
      if (typeof url === 'string' && url.includes('fiscal-years')) return Promise.resolve(pageOf([OPEN_YEAR]))
      if (typeof url === 'string' && url.includes('period-closing-vouchers')) {
        return Promise.resolve(pageOf([DRAFT_VOUCHER, SUBMITTED_VOUCHER]))
      }
      return Promise.resolve({ data: [] })
    })
  })

  it('renders the execution list with status badges and gated actions', async () => {
    render(<PeriodClosingList />)

    expect(await screen.findByText('DRAFT')).toBeInTheDocument()
    expect(screen.getByText('PCV-2025-1')).toBeInTheDocument()
    const table = screen.getByRole('table')
    expect(within(table).getByText('Draft')).toBeInTheDocument()
    expect(within(table).getByText('Submitted')).toBeInTheDocument()
    // Submit only on the Draft row; Cancel only on the Submitted row.
    expect(screen.getAllByRole('button', { name: /Submit/ })).toHaveLength(1)
    expect(screen.getAllByRole('button', { name: 'Cancel' })).toHaveLength(1)
  })

  it('submits idempotently: retries reuse the same Idempotency-Key', async () => {
    const user = userEvent.setup()
    const submitted = { ...DRAFT_VOUCHER, documentStatus: 'Submitted', voucherNo: 'PCV-2025-1' }
    post().mockResolvedValue({ data: submitted })
    render(<PeriodClosingList />)

    await user.click(await screen.findByRole('button', { name: /Submit: DRAFT/ }))
    await waitFor(() => expect(post()).toHaveBeenCalledTimes(1))
    const first = post().mock.calls[0] as [string, object, { headers: Record<string, string> }]
    expect(first[0]).toBe('/v1/period-closing-vouchers/v-draft/submit?companyId=c-1')
    expect(first[1]).toMatchObject({ rowVersion: 'AAA=' })
    const firstKey = first[2].headers['Idempotency-Key']
    expect(firstKey).toBeTruthy()

    // Network-break retry (same intent, list still shows the Draft row): the key
    // MUST be reused so the replay returns the recorded success with zero new
    // GL rows (FC-09).
    await user.click(screen.getByRole('button', { name: /Submit: DRAFT/ }))
    await waitFor(() => expect(post()).toHaveBeenCalledTimes(2))
    const second = post().mock.calls[1] as [string, object, { headers: Record<string, string> }]
    expect(second[2].headers['Idempotency-Key']).toBe(firstKey)
  })

  it('maps the duplicate_closing_for_fiscal_year code to the localized catalog', async () => {
    const user = userEvent.setup()
    post().mockRejectedValue(
      new ApiError(409, 'Conflict', 'already closed', 'duplicate_closing_for_fiscal_year'),
    )
    render(<PeriodClosingList />)

    await user.click(await screen.findByRole('button', { name: /Submit: DRAFT/ }))
    expect(await screen.findByText('A submitted close already exists.')).toBeInTheDocument()
  })

  it('shows the P&L preview with the retained line and net banner', async () => {
    const user = userEvent.setup()
    render(<PeriodClosingList />)

    const previewButtons = await screen.findAllByRole('button', { name: 'Preview' })
    await user.click(previewButtons[0])
    expect(await screen.findByText('P&L preview')).toBeInTheDocument()
    expect(screen.getByText('4000 - Revenue')).toBeInTheDocument()
    expect(screen.getByText(/3100 - Retained Earnings/)).toBeInTheDocument()
    expect(screen.getByText('Net profit 120,000.00')).toBeInTheDocument()
    expect(get()).toHaveBeenCalledWith(
      '/v1/period-closing-vouchers/unclosed-balances',
      expect.objectContaining({ params: { companyId: 'c-1', fiscalYearId: 'y-open' } }),
    )
  })

  it('cancels a submitted voucher through the confirm step with reversal semantics', async () => {
    const user = userEvent.setup()
    post().mockResolvedValue({ data: { ...SUBMITTED_VOUCHER, documentStatus: 'Cancelled' } })
    render(<PeriodClosingList />)

    await user.click(await screen.findByRole('button', { name: 'Cancel' }))
    // Confirm step appears before any POST.
    expect(post()).not.toHaveBeenCalled()
    await user.click(await screen.findByRole('button', { name: 'Cancel: PCV-2025-1' }))
    await waitFor(() => expect(post()).toHaveBeenCalledTimes(1))
    const [url] = post().mock.calls[0] as [string]
    expect(url).toBe('/v1/period-closing-vouchers/v-sub/cancel?companyId=c-1')
  })
})
