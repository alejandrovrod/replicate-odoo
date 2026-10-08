import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { FiscalYearList } from './FiscalYearList'
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

const CLOSED_YEAR = {
  id: 'y-closed',
  companyId: 'c-1',
  yearName: 'FY-2024',
  startDate: '2024-01-01',
  endDate: '2024-12-31',
  isClosed: true,
  closedAt: '2025-01-05T00:00:00Z',
  rowVersion: 'BBB=',
}

const pageOf = (items: unknown[]) => ({
  data: { items, totalCount: items.length, pageNumber: 1, pageSize: 50, totalPages: 1 },
})

describe('FiscalYearList (R-13 Task 5.1)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle(
      'en',
      'accounting',
      {
        fiscalYear: {
          title: 'Fiscal Years',
          search: 'Search fiscal years...',
          filterAll: 'All years',
          filterOpen: 'Open',
          filterClosed: 'Closed',
          colStatus: 'Status',
          statusClosed: 'Closed',
          statusOpen: 'Open',
          newBtn: 'New Fiscal Year',
          closeBtn: 'Close Year',
          confirmClose: 'Close year',
          empty: 'No fiscal years yet for this company.',
        },
        fiscalYearForm: {
          titleNew: 'New Fiscal Year',
          nameLabel: 'Year Name *',
          startLabel: 'Start Date *',
          endLabel: 'End Date *',
          cancel: 'Cancel',
          save: 'Save',
          errorRequired: 'Year name, start date and end date are required.',
          errorDateOrder: 'Start date must be before end date.',
        },
        periodClosing: { colActions: 'Actions' },
      },
      true,
      true,
    )
    i18n.addResourceBundle(
      'en',
      'error',
      { fiscal_year_closed: 'The fiscal year is closed and immutable.' },
      true,
      true,
    )
    await i18n.changeLanguage('en')
    useTenantStore.setState({ companyId: 'c-1' })
    get().mockReset()
    post().mockReset()
    get().mockImplementation((url: string) =>
      Promise.resolve(
        typeof url === 'string' && url.includes('fiscal-years') ? pageOf([]) : { data: [] },
      ),
    )
  })

  it('renders the master list and hides the close action on closed years', async () => {
    get().mockImplementation((url: string) =>
      Promise.resolve(
        typeof url === 'string' && url.includes('fiscal-years')
          ? pageOf([OPEN_YEAR, CLOSED_YEAR])
          : { data: [] },
      ),
    )
    render(<FiscalYearList />)

    expect(await screen.findByText('FY-2025')).toBeInTheDocument()
    expect(screen.getByText('FY-2024')).toBeInTheDocument()
    // Exactly one close button: the open year. The closed year renders locked.
    expect(screen.getAllByRole('button', { name: 'Close Year' })).toHaveLength(1)
    expect(within(screen.getByRole('table')).getByText('Closed')).toBeInTheDocument()
  })

  it('validates the create form (required + Start < End) before posting', async () => {
    const user = userEvent.setup()
    render(<FiscalYearList />)
    await user.click(await screen.findByRole('button', { name: 'New Fiscal Year' }))

    // Empty submit → required error, no POST.
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(
      await screen.findByText('Year name, start date and end date are required.'),
    ).toBeInTheDocument()
    expect(post()).not.toHaveBeenCalled()

    // Start >= End → date-order error, no POST.
    await user.type(screen.getByLabelText(/Year Name/), 'FY-2026')
    await user.type(screen.getByLabelText(/Start Date/), '2026-12-31')
    await user.type(screen.getByLabelText(/End Date/), '2026-01-01')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    expect(await screen.findByText('Start date must be before end date.')).toBeInTheDocument()
    expect(post()).not.toHaveBeenCalled()
  })

  it('creates a year and closes it with RowVersion + Idempotency-Key', async () => {
    const user = userEvent.setup()
    get().mockImplementation((url: string) => Promise.resolve(typeof url === 'string' && url.includes('fiscal-years') ? pageOf([OPEN_YEAR]) : { data: [] }))
    post().mockResolvedValue({ data: OPEN_YEAR })
    render(<FiscalYearList />)

    // Create flow posts the payload with an idempotency key.
    await user.click(await screen.findByRole('button', { name: 'New Fiscal Year' }))
    await user.type(screen.getByLabelText(/Year Name/), 'FY-2025')
    await user.type(screen.getByLabelText(/Start Date/), '2025-01-01')
    await user.type(screen.getByLabelText(/End Date/), '2025-12-31')
    await user.click(screen.getByRole('button', { name: 'Save' }))
    await waitFor(() => expect(post()).toHaveBeenCalledTimes(1))
    const [url, body, config] = post().mock.calls[0] as [string, object, { headers: object }]
    expect(url).toBe('/v1/fiscal-years')
    expect(body).toMatchObject({ companyId: 'c-1', yearName: 'FY-2025' })
    expect(config.headers).toHaveProperty('Idempotency-Key')

    // Close flow: confirm step → POST …/close carrying the current RowVersion.
    await user.click(screen.getByRole('button', { name: 'Close Year' }))
    await user.click(await screen.findByRole('button', { name: /Close year: FY-2025/ }))
    await waitFor(() => expect(post()).toHaveBeenCalledTimes(2))
    const [closeUrl, closeBody] = post().mock.calls[1] as [string, object]
    expect(closeUrl).toBe('/v1/fiscal-years/y-open/close')
    expect(closeBody).toMatchObject({ companyId: 'c-1', rowVersion: 'AAA=' })
  })

  it('maps the fiscal_year_closed code to the localized catalog', async () => {
    const user = userEvent.setup()
    get().mockImplementation((url: string) => Promise.resolve(typeof url === 'string' && url.includes('fiscal-years') ? pageOf([OPEN_YEAR]) : { data: [] }))
    post().mockRejectedValue(new ApiError(409, 'Conflict', 'closed', 'fiscal_year_closed'))
    render(<FiscalYearList />)

    await user.click(await screen.findByRole('button', { name: 'Close Year' }))
    await user.click(await screen.findByRole('button', { name: /Close year: FY-2025/ }))
    expect(
      await screen.findByText('The fiscal year is closed and immutable.'),
    ).toBeInTheDocument()
  })

  it('filters open vs closed server-side through the IsClosed param', async () => {
    const user = userEvent.setup()
    get().mockImplementation((url: string) => Promise.resolve(typeof url === 'string' && url.includes('fiscal-years') ? pageOf([OPEN_YEAR]) : { data: [] }))
    render(<FiscalYearList />)
    await screen.findByText('FY-2025')

    await user.click(screen.getByRole('button', { name: 'Closed' }))
    await waitFor(() => {
      const calls = get().mock.calls as [string, { params: Record<string, unknown> }][]
      const last = calls[calls.length - 1]
      expect(last[0]).toBe('/v1/fiscal-years')
      expect(last[1].params).toMatchObject({ isClosed: true })
    })
    expect(within(screen.getByRole('group')).getByRole('button', { name: 'Closed' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })
})
