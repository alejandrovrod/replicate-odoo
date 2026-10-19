import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { SalarySlipPrintView } from './SalarySlipPrintView'
import { i18n, loadTestFixtures } from '../../test/i18n-fixtures'
import type { SalarySlipPrintable } from './types'

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

const DATA: SalarySlipPrintable = {
  slipId: 'slip-1',
  slipNumber: 'SLIP-2025-00001',
  payrollEntryId: 'pe-1',
  payrollNumber: 'PE-2025-00001',
  periodStart: '2025-03-01',
  periodEnd: '2025-03-31',
  postingDate: '2025-03-31',
  status: 'Submitted',
  paymentDays: 30,
  absentDays: 0,
  company: { name: 'Acme Industrial', taxId: 'ACME-0001', countryCode: 'MX' },
  employee: { employeeNumber: 'EMP-0001', fullName: 'Ana Reyes', workEmail: 'ana.reyes@example.com' },
  grossPay: 5200,
  totalDeductions: 400,
  netPay: 4800,
  earnings: [
    { componentName: 'Basic Salary', componentType: 'Earning', amount: 5000 },
    { componentName: 'Housing Allowance', componentType: 'Earning', amount: 200 },
  ],
  deductions: [
    { componentName: 'Tax', componentType: 'Deduction', amount: 300 },
    { componentName: 'Pension', componentType: 'Deduction', amount: 100 },
  ],
}

const HR_BUNDLE = {
  salarySlip: {
    documentTitle: 'Pay stub',
    taxId: 'Tax ID',
    employee: 'Employee',
    employeeCode: 'Employee no.',
    email: 'Email',
    period: 'Period',
    settlementDate: 'Settlement date',
    paymentDays: 'Paid days',
    absentDays: 'Absent days',
    earnings: 'Earnings',
    deductions: 'Deductions',
    component: 'Component',
    amount: 'Amount',
    totalEarnings: 'Total earnings',
    totalDeductions: 'Total deductions',
    emptySection: 'No lines',
    netPay: 'Net pay',
    netInWords: 'In words: {{words}}.',
    signature: 'Signature',
    statusDraft: 'Draft',
    statusSubmitted: 'Submitted',
    statusCancelled: 'Cancelled',
  },
}

describe('SalarySlipPrintView (printable pay stub)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle('en', 'hr-payroll', HR_BUNDLE, true, true)
  })

  it('renders the company fiscal header, the employee block and the period', () => {
    render(<SalarySlipPrintView data={DATA} />)

    expect(screen.getByText('Acme Industrial')).toBeInTheDocument()
    expect(screen.getByText('ACME-0001')).toBeInTheDocument()
    expect(screen.getByText('Pay stub')).toBeInTheDocument()
    expect(screen.getByText('SLIP-2025-00001')).toBeInTheDocument()
    expect(screen.getByText('Submitted')).toBeInTheDocument()

    expect(screen.getByText('Ana Reyes')).toBeInTheDocument()
    expect(screen.getByText('EMP-0001')).toBeInTheDocument()
    expect(screen.getByText('ana.reyes@example.com')).toBeInTheDocument()
    expect(screen.getByText('2025-03-01…2025-03-31')).toBeInTheDocument()
  })

  it('splits earnings and deductions with their totals', () => {
    render(<SalarySlipPrintView data={DATA} />)

    expect(screen.getByText('Basic Salary')).toBeInTheDocument()
    expect(screen.getByText('Housing Allowance')).toBeInTheDocument()
    expect(screen.getByText('Tax')).toBeInTheDocument()
    expect(screen.getByText('Pension')).toBeInTheDocument()

    expect(screen.getByText('Total earnings')).toBeInTheDocument()
    expect(screen.getByText('Total deductions')).toBeInTheDocument()
    expect(screen.getByText('$5,200.00')).toBeInTheDocument()
    expect(screen.getByText('$400.00')).toBeInTheDocument()
  })

  it('prints the net pay in numbers and in words', () => {
    render(<SalarySlipPrintView data={DATA} />)

    expect(screen.getByText('Net pay')).toBeInTheDocument()
    expect(screen.getByText('$4,800.00')).toBeInTheDocument()
    expect(
      screen.getByText('In words: four thousand eight hundred dollars.'),
    ).toBeInTheDocument()
  })
})
