import { useTranslation } from 'react-i18next'
import { amountInWords } from '../../lib/amountInWords'
import { displayLocale, formatMoney } from '../../lib/format'
import type { SalarySlipPrintable, SalarySlipStatus } from './types'

const STATUS_KEY: Record<SalarySlipStatus, string> = {
  Draft: 'salarySlip.statusDraft',
  Submitted: 'salarySlip.statusSubmitted',
  Cancelled: 'salarySlip.statusCancelled',
}

const TOTAL_KEY = {
  earnings: 'salarySlip.totalEarnings',
  deductions: 'salarySlip.totalDeductions',
  empty: 'salarySlip.emptySection',
} as const

/**
 * Formal pay-stub layout (spec: receipt view). Purely presentational - it renders whatever
 * `SalarySlipPrintable` it receives, so it is trivially snapshot-able and the printable server
 * contract stays the single source of truth. The root carries `salary-slip-printable`, which the
 * `@media print` rules in `index.css` reveal while hiding the rest of the page.
 */
export function SalarySlipPrintView({ data }: { data: SalarySlipPrintable }) {
  const { t, i18n } = useTranslation('hr-payroll')
  const locale = displayLocale()

  return (
    <div className="salary-slip-printable rounded-lg border border-slate-300 bg-white p-6 text-slate-900">
      <div className="flex flex-wrap items-start justify-between gap-3 border-b border-slate-300 pb-4">
        <div>
          <h2 className="text-lg font-bold">{data.company.name}</h2>
          <p className="text-xs text-slate-600">
            {t('salarySlip.taxId')}: <span className="font-mono">{data.company.taxId || '—'}</span>
          </p>
          {data.company.countryCode ? (
            <p className="text-xs text-slate-500">{data.company.countryCode}</p>
          ) : null}
        </div>
        <div className="text-right">
          <p className="text-sm font-semibold uppercase tracking-wide">{t('salarySlip.documentTitle')}</p>
          <p className="font-mono text-xs text-slate-600">{data.slipNumber}</p>
          <span className="mt-1 inline-block rounded bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-700">
            {t(STATUS_KEY[data.status] as 'salarySlip.statusDraft')}
          </span>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 border-b border-slate-200 py-4 text-xs sm:grid-cols-2">
        <dl className="space-y-1">
          <div>
            <dt className="inline font-semibold">{t('salarySlip.employee')}: </dt>
            <dd className="inline">{data.employee.fullName || '—'}</dd>
          </div>
          <div>
            <dt className="inline font-semibold">{t('salarySlip.employeeCode')}: </dt>
            <dd className="inline font-mono">{data.employee.employeeNumber || '—'}</dd>
          </div>
          <div>
            <dt className="inline font-semibold">{t('salarySlip.email')}: </dt>
            <dd className="inline">{data.employee.workEmail || '—'}</dd>
          </div>
        </dl>
        <dl className="space-y-1 sm:text-right">
          <div>
            <dt className="inline font-semibold">{t('salarySlip.period')}: </dt>
            <dd className="inline font-mono">{data.periodStart}…{data.periodEnd}</dd>
          </div>
          <div>
            <dt className="inline font-semibold">{t('salarySlip.settlementDate')}: </dt>
            <dd className="inline font-mono">{data.postingDate}</dd>
          </div>
          <div>
            <dt className="inline font-semibold">{t('salarySlip.paymentDays')}: </dt>
            <dd className="inline font-mono">{data.paymentDays}</dd>
            <dt className="ml-3 inline font-semibold">{t('salarySlip.absentDays')}: </dt>
            <dd className="inline font-mono">{data.absentDays}</dd>
          </div>
        </dl>
      </div>

      <div className="grid grid-cols-1 gap-6 py-4 sm:grid-cols-2">
        <LineTable
          title={t('salarySlip.earnings')}
          totalKey={TOTAL_KEY.earnings}
          emptyKey={TOTAL_KEY.empty}
          lines={data.earnings}
          total={data.grossPay}
          locale={locale}
        />
        <LineTable
          title={t('salarySlip.deductions')}
          totalKey={TOTAL_KEY.deductions}
          emptyKey={TOTAL_KEY.empty}
          lines={data.deductions}
          total={data.totalDeductions}
          locale={locale}
        />
      </div>

      <div className="border-t-2 border-slate-800 pt-4">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <span className="text-sm font-bold uppercase">{t('salarySlip.netPay')}</span>
          <span className="font-mono text-xl font-bold">{formatMoney(data.netPay)}</span>
        </div>
        <p className="mt-1 text-xs italic text-slate-600">
          {t('salarySlip.netInWords', { words: amountInWords(data.netPay, i18n.language ?? 'en') })}
        </p>
      </div>

      <div className="mt-10 flex justify-end">
        <div className="w-56 border-t border-slate-400 pt-1 text-center text-[11px] text-slate-500">
          {t('salarySlip.signature')}
        </div>
      </div>
    </div>
  )
}

function LineTable({
  title,
  totalKey,
  emptyKey,
  lines,
  total,
  locale,
}: {
  title: string
  totalKey: string
  emptyKey: string
  lines: SalarySlipPrintable['earnings']
  total: number
  locale: string
}) {
  const { t } = useTranslation('hr-payroll')

  return (
    <table className="w-full text-left text-xs">
      <caption className="pb-1 text-left text-sm font-semibold">{title}</caption>
      <thead>
        <tr className="border-b border-slate-300 text-slate-500">
          <th className="py-1.5 font-semibold">{t('salarySlip.component')}</th>
          <th className="py-1.5 text-right font-semibold">{t('salarySlip.amount')}</th>
        </tr>
      </thead>
      <tbody className="divide-y divide-slate-100">
        {lines.length === 0 ? (
          <tr>
            <td className="py-1.5 text-slate-400" colSpan={2}>
              {t(emptyKey as 'salarySlip.emptySection')}
            </td>
          </tr>
        ) : (
          lines.map((line) => (
            <tr key={`${line.componentType}-${line.componentName}`}>
              <td className="py-1.5">{line.componentName}</td>
              <td className="py-1.5 text-right font-mono">{line.amount.toLocaleString(locale, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}</td>
            </tr>
          ))
        )}
      </tbody>
      <tfoot>
        <tr className="border-t border-slate-300 font-semibold">
          <td className="py-1.5">{t(totalKey as 'salarySlip.totalEarnings')}</td>
          <td className="py-1.5 text-right font-mono">{formatMoney(total)}</td>
        </tr>
      </tfoot>
    </table>
  )
}
