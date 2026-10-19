import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { LeadsManagementModal } from './LeadsManagementModal'
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
const post = () => apiClient.post as unknown as Mock

const OPEN_LEAD = {
  id: 'lead-1',
  companyId: 'c-1',
  leadCode: 'LEAD-1',
  leadName: 'Ada Lovelace',
  organizationName: 'Acme Corp',
  email: 'ada@acme.test',
  phone: '+54 11 5555 5555',
  source: 'Manual / Ventas',
  status: 'Open',
}

const CONVERTED_LEAD = {
  ...OPEN_LEAD,
  id: 'lead-2',
  leadCode: 'LEAD-2',
  leadName: 'Grace Hopper',
  status: 'Converted',
}

const CONVERT_RESULT = {
  leadId: 'lead-1',
  customerId: 'cust-1',
  customerCode: 'ACME-CORP',
  opportunityId: 'opp-1',
  opportunityNumber: 'OPP-2026-00001',
  weightedPipelineAmount: 1000,
}

function mockLeads(leads = [OPEN_LEAD, CONVERTED_LEAD]) {
  get().mockImplementation((url: string) => {
    if (url === '/v1/leads') {
      return Promise.resolve({ data: { items: leads, totalCount: leads.length } })
    }
    return Promise.reject(new Error(`unexpected GET ${url}`))
  })
}

describe('LeadsManagementModal (CRM leads list + conversion)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    get().mockReset()
    post().mockReset()
    mockLeads()
  })

  it('lists leads and offers conversion only for Open ones', async () => {
    render(
      <LeadsManagementModal open initialView="list" onClose={() => {}} onChanged={() => {}} />,
    )

    expect(await screen.findByText('LEAD-1')).toBeInTheDocument()
    expect(screen.getByText('LEAD-2')).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: 'Convert to Opportunity' })).toHaveLength(1)
  })

  it('converts an Open lead and notifies the overview to reload', async () => {
    post().mockResolvedValue({ data: CONVERT_RESULT })
    const user = userEvent.setup()
    const onChanged = vi.fn()

    render(
      <LeadsManagementModal open initialView="list" onClose={() => {}} onChanged={onChanged} />,
    )
    await screen.findByText('LEAD-1')

    await user.click(screen.getByRole('button', { name: 'Convert to Opportunity' }))
    expect(await screen.findByRole('heading', { name: 'Convert to Opportunity' })).toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: 'Convert' }))

    await waitFor(() => expect(post()).toHaveBeenCalled())
    const [url] = post().mock.calls[0] as [string]
    expect(url).toBe('/v1/leads/lead-1/convert')
    expect(onChanged).toHaveBeenCalled()
  })

  it('creates a manual lead from the quick intake form', async () => {
    post().mockResolvedValue({ data: { lead: { ...OPEN_LEAD, leadCode: 'LEAD-NEW' }, duplicate: false } })
    const user = userEvent.setup()
    const onChanged = vi.fn()

    render(
      <LeadsManagementModal open initialView="form" onClose={() => {}} onChanged={onChanged} />,
    )

    await user.type(await screen.findByLabelText('Lead name'), 'New Prospect')
    await user.type(screen.getByLabelText('Company (optional)'), 'Globex')
    await user.click(screen.getByRole('button', { name: 'Create Lead' }))

    await waitFor(() => expect(post()).toHaveBeenCalled())
    const [url, body] = post().mock.calls[0] as [string, Record<string, unknown>]
    expect(url).toBe('/v1/leads/ingest')
    expect(body).toMatchObject({
      leadName: 'New Prospect',
      organizationName: 'Globex',
      source: 'Manual / Ventas',
    })
    expect(body.leadCode).toMatch(/^LEAD-/)
    expect(onChanged).toHaveBeenCalled()
  })
})
