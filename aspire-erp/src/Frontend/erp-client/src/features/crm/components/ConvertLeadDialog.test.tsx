import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { ConvertLeadDialog, suggestCustomerCode } from './ConvertLeadDialog'
import { ApiError, apiClient } from '../../../api/client'
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

const post = () => apiClient.post as unknown as Mock

const LEAD = {
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

const RESULT = {
  leadId: 'lead-1',
  customerId: 'cust-1',
  customerCode: 'ACME-CORP',
  opportunityId: 'opp-1',
  opportunityNumber: 'OPP-2026-00001',
  weightedPipelineAmount: 1000,
}

describe('ConvertLeadDialog (CRM-03 conversion)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    post().mockReset()
  })

  it('pre-suggests the customer code and converts with the chosen terms', async () => {
    post().mockResolvedValue({ data: RESULT })
    const user = userEvent.setup()
    const onConverted = vi.fn()

    render(<ConvertLeadDialog lead={LEAD} onClose={() => {}} onConverted={onConverted} />)

    expect(screen.getByText('Ada Lovelace')).toBeInTheDocument()
    expect(screen.getByText('Acme Corp')).toBeInTheDocument()
    expect(screen.getByLabelText('Customer code')).toHaveValue('ACME-CORP')
    expect(screen.getByLabelText('Probability %')).toHaveValue(25)
    expect(screen.getByLabelText('Payment terms (days)')).toHaveValue(30)

    await user.click(screen.getByRole('button', { name: 'Convert' }))

    await waitFor(() => expect(post()).toHaveBeenCalled())
    const [url, body] = post().mock.calls[0] as [string, Record<string, unknown>]
    expect(url).toBe('/v1/leads/lead-1/convert')
    expect(body).toMatchObject({
      customerCode: 'ACME-CORP',
      opportunityAmount: 0,
      opportunityProbability: 25,
      paymentTermsDays: 30,
    })
    expect(onConverted).toHaveBeenCalledWith(RESULT)
  })

  it('derives the customer code from the company name or falls back to CLI-', () => {
    expect(suggestCustomerCode({ organizationName: 'Globex Industries' })).toBe('GLOBEX-INDUSTRIES')
    expect(suggestCustomerCode({ organizationName: null })).toMatch(/^CLI-[A-Z0-9]{6}$/)
  })

  it('surfaces a duplicate-code rejection and never reports success', async () => {
    post().mockRejectedValue(
      new ApiError(409, 'Conflict', 'Customer code already in use', 'crm_customer_code_exists'),
    )
    const user = userEvent.setup()
    const onConverted = vi.fn()

    render(<ConvertLeadDialog lead={LEAD} onClose={() => {}} onConverted={onConverted} />)
    await user.click(screen.getByRole('button', { name: 'Convert' }))

    expect(await screen.findByText('Customer code already in use')).toBeInTheDocument()
    expect(onConverted).not.toHaveBeenCalled()
  })
})
