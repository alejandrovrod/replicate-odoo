import { useTranslation } from 'react-i18next'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table'
import { calculateTaxes, type TaxRowInput } from '../taxCalculation'

/** One breakdown row: engine input plus the GL account label it hits. */
export interface TaxBreakdownRow extends TaxRowInput {
  accountLabel: string
}

interface TaxBreakdownTableProps {
  netBase: number
  rows: TaxBreakdownRow[]
  decimals?: number
}

/**
 * Live tax breakdown (tax-engine Fase 2, gap D5): taxable base, rate, type
 * (Traslado/Retención), computed signed amount and the affected GL account, plus the
 * Add/Deduct totals and the grand total. Pure preview - the server recomputes on save.
 */
export function TaxBreakdownTable({ netBase, rows, decimals = 2 }: TaxBreakdownTableProps) {
  const { t } = useTranslation('accounting')
  const calc = calculateTaxes(netBase, rows, decimals)

  if (rows.length === 0) {
    return (
      <p className="text-xs text-slate-400">
        {t('tax.noRows', 'No taxes - the document posts its net amount only.')}
      </p>
    )
  }

  return (
    <div className="space-y-2">
      <div className="rounded-lg border border-slate-200">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('tax.colAccount', 'Account')}</TableHead>
              <TableHead className="text-right">{t('tax.colBase', 'Taxable Base')}</TableHead>
              <TableHead className="text-right">{t('tax.colRate', 'Rate %')}</TableHead>
              <TableHead>{t('tax.colType', 'Type')}</TableHead>
              <TableHead className="text-right">{t('tax.colAmount', 'Amount')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {calc.rows.map((row, i) => (
              <TableRow key={i}>
                <TableCell className="text-slate-800">{rows[i].accountLabel}</TableCell>
                <TableCell className="text-right font-mono text-slate-600">
                  {row.basis === 'FixedAmount' ? '—' : row.base.toLocaleString()}
                </TableCell>
                <TableCell className="text-right font-mono text-slate-600">
                  {row.basis === 'FixedAmount'
                    ? t('tax.fixed', 'fixed')
                    : `${row.rate.toLocaleString()}%${row.basis === 'OnPreviousRow' ? ' ⟳' : ''}`}
                </TableCell>
                <TableCell>
                  <span
                    className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                      row.chargeType === 'Add'
                        ? 'bg-sky-100 text-sky-800'
                        : 'bg-amber-100 text-amber-800'
                    }`}
                  >
                    {row.chargeType === 'Add'
                      ? t('tax.chargeAdd', 'Traslado')
                      : t('tax.chargeDeduct', 'Retención')}
                  </span>
                </TableCell>
                <TableCell
                  className={`text-right font-mono font-semibold ${
                    row.amount < 0 ? 'text-amber-700' : 'text-slate-900'
                  }`}
                >
                  {row.amount.toLocaleString()}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
      <div className="space-y-1 rounded-lg border border-slate-200 bg-white p-3 font-mono text-xs">
        <div className="flex justify-between text-slate-600">
          <span>{t('tax.totalAdd', 'Transferred taxes')}</span>
          <span>+{calc.addTotal.toLocaleString()}</span>
        </div>
        <div className="flex justify-between text-amber-700">
          <span>{t('tax.totalDeduct', 'Retentions')}</span>
          <span>−{calc.deductTotal.toLocaleString()}</span>
        </div>
        <div className="flex justify-between border-t border-slate-100 pt-1 text-sm font-bold text-slate-900">
          <span>{t('tax.grandTotal', 'Net payable')}</span>
          <span>{calc.grandTotal.toLocaleString()}</span>
        </div>
      </div>
    </div>
  )
}
