import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { FiscalYearList } from './FiscalYearList'
import { PeriodClosingList } from './PeriodClosingList'

type ClosingTab = 'years' | 'vouchers'

/**
 * R-13 Phase 5 entry view (route `accounting-period-closing`): fiscal-year master
 * plus the closing-voucher execution screen behind tabs.
 */
export function FiscalClosingView() {
  const { t } = useTranslation('accounting')
  const [tab, setTab] = useState<ClosingTab>('years')

  return (
    <div className="space-y-6">
      <div role="tablist" aria-label={t('periodClosing.title')} className="flex gap-2">
        {(['years', 'vouchers'] as const).map((value) => (
          <button
            key={value}
            role="tab"
            type="button"
            aria-selected={tab === value}
            onClick={() => setTab(value)}
            className={`rounded-md border px-4 py-2 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-sky-600 focus-visible:ring-offset-2 ${
              tab === value
                ? 'border-sky-600 bg-sky-600 text-white'
                : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-50'
            }`}
          >
            {value === 'years' ? t('fiscalYear.title') : t('periodClosing.title')}
          </button>
        ))}
      </div>
      <div role="tabpanel">
        {tab === 'years' ? <FiscalYearList /> : <PeriodClosingList />}
      </div>
    </div>
  )
}
