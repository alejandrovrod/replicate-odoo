import { ArrowDownRight, ArrowUpRight, type DollarSign } from 'lucide-react'
import { useTranslation } from 'react-i18next'

export interface KpiCard {
  key: string
  title: string
  value: string
  sub: string
  trend: 'up' | 'down' | 'flat'
  icon: typeof DollarSign
  color: string
}

export function KpiCardsGrid({ cards, loading }: { cards: KpiCard[]; loading: boolean }) {
  const { t } = useTranslation('dashboard')
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {cards.map((kpi) => {
        const Icon = kpi.icon
        return (
          <div key={kpi.key} className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
            <div className="flex items-center justify-between">
              <span className="text-xs font-medium text-slate-500">{kpi.title}</span>
              <div className={`rounded-lg p-2 ${kpi.color}`}>
                <Icon className="h-5 w-5" />
              </div>
            </div>
            <div className="mt-4 flex items-baseline justify-between">
              <span className="text-2xl font-bold tracking-tight text-slate-900">
                {loading ? '…' : kpi.value}
              </span>
              <span
                className={`flex items-center text-xs font-semibold ${
                  kpi.trend === 'up'
                    ? 'text-emerald-600'
                    : kpi.trend === 'down'
                      ? 'text-rose-600'
                      : 'text-slate-400'
                }`}
              >
                {kpi.trend === 'up' ? (
                  <ArrowUpRight className="mr-0.5 h-3.5 w-3.5" />
                ) : kpi.trend === 'down' ? (
                  <ArrowDownRight className="mr-0.5 h-3.5 w-3.5" />
                ) : null}
                {kpi.sub || t('kpi.live', 'live')}
              </span>
            </div>
          </div>
        )
      })}
    </div>
  )
}
