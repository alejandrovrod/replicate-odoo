import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { PaymentEntryDetailDialog } from './PaymentEntryDetailDialog'
import { apiClient } from '../../../api/client'
import { i18n, loadTestFixtures } from '../../../test/i18n-fixtures'

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

const SUBMITTED_DETAIL = {
  payment: {
    id: 'p1',
    companyId: 'c-1',
    voucherNo: 'PAY-2026-00001',
    paymentType: 'Receive',
    partyType: 'Customer',
    partyId: 'cust-1',
    bankAccountId: 'b1',
    paymentDate: '2026-10-05',
    paidAmount: 100,
    unallocatedAmount: 0,
    documentStatus: 'Submitted',
    status: 'Unreconciled',
    partyName: 'ACME Corp',
    modeOfPayment: 'Wire',
    remarks: '',
  },
  allocations: [
    {
      id: 'a1',
      salesInvoiceId: 'inv-01',
      purchaseInvoiceId: null,
      allocatedAmount: 100,
      referenceDocumentType: 'SalesInvoice',
      referenceDocumentId: 'inv-01',
      totalAmount: 100,
      outstandingAmount: 0,
      exchangeRate: 1,
    },
  ],
  glEntries: [
    {
      id: 1,
      accountId: 'acc-cash',
      accountCode: '1100',
      accountName: 'Cash - Bank',
      postingDate: '2026-10-05',
      debit: 100,
      credit: 0,
      voucherType: 'PaymentEntry',
      voucherNo: 'PAY-2026-00001',
      remarks: 'Dr Bank',
    },
    {
      id: 2,
      accountId: 'acc-ar',
      accountCode: '1200',
      accountName: 'Accounts Receivable',
      postingDate: '2026-10-05',
      debit: 0,
      credit: 100,
      voucherType: 'PaymentEntry',
      voucherNo: 'PAY-2026-00001',
      remarks: 'Cr Customer',
    },
  ],
}

describe('PaymentEntryDetailDialog (ui-spec r-12 D1 / PAY-UI-G3)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle('en', 'banking', { payments: {} }, true, true)
    await i18n.changeLanguage('en')
    get().mockReset()
  })

  it('shows the header and the posted GL lines under the Accounting Entry tab', async () => {
    const user = userEvent.setup()
    get().mockResolvedValue({ data: SUBMITTED_DETAIL })

    render(
      <PaymentEntryDetailDialog isOpen paymentId="p1" companyId="c-1" onClose={() => {}} />,
    )

    // General tab is the landing view.
    expect(await screen.findByText('PAY-2026-00001')).toBeInTheDocument()
    expect(get()).toHaveBeenCalledWith('/v1/PaymentEntries/p1', { params: { companyId: 'c-1' } })
    expect(screen.getByText('ACME Corp')).toBeInTheDocument()

    await user.click(screen.getByRole('tab', { name: 'Accounting Entry' }))

    await waitFor(() => expect(screen.getByText('1100')).toBeInTheDocument())
    expect(screen.getByText('Cash - Bank')).toBeInTheDocument()
    expect(screen.getByText('1200')).toBeInTheDocument()
    expect(screen.getByText('Balanced')).toBeInTheDocument()
  })

  it('lists the settled invoice under the Allocated Invoices tab', async () => {
    const user = userEvent.setup()
    get().mockResolvedValue({ data: SUBMITTED_DETAIL })

    render(
      <PaymentEntryDetailDialog isOpen paymentId="p1" companyId="c-1" onClose={() => {}} />,
    )

    await screen.findByText('PAY-2026-00001')
    await user.click(screen.getByRole('tab', { name: 'Allocated Invoices' }))

    expect(await screen.findByText('inv-01')).toBeInTheDocument()
    expect(screen.getByText('SalesInvoice')).toBeInTheDocument()
  })

  it('explains the empty ledger for a draft voucher', async () => {
    const user = userEvent.setup()
    get().mockResolvedValue({
      data: {
        ...SUBMITTED_DETAIL,
        payment: { ...SUBMITTED_DETAIL.payment, voucherNo: '', documentStatus: 'Draft' },
        allocations: [],
        glEntries: [],
      },
    })

    render(
      <PaymentEntryDetailDialog isOpen paymentId="p2" companyId="c-1" onClose={() => {}} />,
    )

    await screen.findByText('Payment Entry (draft)')
    await user.click(screen.getByRole('tab', { name: 'Accounting Entry' }))

    expect(
      await screen.findByText('No ledger lines yet — the voucher is still a draft.'),
    ).toBeInTheDocument()
  })
})
