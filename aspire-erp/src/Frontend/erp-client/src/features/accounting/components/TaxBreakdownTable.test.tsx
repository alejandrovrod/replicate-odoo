import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { TaxBreakdownTable } from './TaxBreakdownTable'
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

describe('TaxBreakdownTable (tax-engine Fase 2, gap D5)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle('en', 'accounting', { tax: {} }, true, true)
    await i18n.changeLanguage('en')
  })

  it('shows the signed breakdown with Add/Deduct totals (TAX-G1)', () => {
    render(
      <TaxBreakdownTable
        netBase={10000}
        rows={[
          { rate: 16, chargeType: 'Add', basis: 'OnNetTotal', accountLabel: '2108 — IVA Trasladado' },
          { rate: 10, chargeType: 'Deduct', basis: 'OnNetTotal', accountLabel: '2109 — Retención ISR' },
          { rate: 10.667, chargeType: 'Deduct', basis: 'OnNetTotal', accountLabel: '2110 — Retención IVA' },
        ]}
      />,
    )

    expect(screen.getByText('2108 — IVA Trasladado')).toBeInTheDocument()
    expect(screen.getAllByText('Retención')).toHaveLength(2)
    expect(screen.getAllByText('9,533.3').length).toBeGreaterThan(0)
  })

  it('renders the empty state without rows', () => {
    render(<TaxBreakdownTable netBase={1000} rows={[]} />)
    expect(screen.getByText('No taxes - the document posts its net amount only.')).toBeInTheDocument()
  })
})
