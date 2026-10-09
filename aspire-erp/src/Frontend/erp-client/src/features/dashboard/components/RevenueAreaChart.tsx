import { useMemo } from 'react'
import { useTranslation } from 'react-i18next'

export interface MonthlyPoint {
  label: string
  revenue: number
  expenses: number
}

/**
 * Dependency-free SVG area chart (no Recharts: the repo keeps zero chart dependencies):
 * monthly revenue vs expenses with gradient fills, gridlines and a value axis.
 */
export function RevenueAreaChart({ points, loading }: { points: MonthlyPoint[]; loading: boolean }) {
  const { t } = useTranslation('dashboard')

  const geometry = useMemo(() => {
    const W = 720
    const H = 260
    const padL = 56
    const padR = 16
    const padT = 16
    const padB = 32
    const max = Math.max(1, ...points.flatMap((p) => [p.revenue, p.expenses]))
    const x = (i: number) =>
      points.length <= 1 ? W / 2 : padL + ((W - padL - padR) * i) / (points.length - 1)
    const y = (v: number) => padT + (H - padT - padB) * (1 - v / max)
    const line = (pick: (p: MonthlyPoint) => number) =>
      points.map((p, i) => `${i === 0 ? 'M' : 'L'}${x(i).toFixed(1)},${y(pick(p)).toFixed(1)}`).join(' ')
    const ticks = [0, 0.25, 0.5, 0.75, 1].map((f) => max * f)
    return { W, H, padL, padR, padT, padB, x, y, line, ticks, max }
  }, [points])

  const { W, H, padL, padR, padB, x, y, line, ticks } = geometry

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-semibold text-slate-900">{t('chart.title', 'Revenue vs Expenses')}</h3>
          <p className="text-xs text-slate-500">{t('chart.subtitle', 'Last 6 months, per-month P&L')}</p>
        </div>
        <div className="flex items-center gap-4 text-xs font-medium">
          <span className="flex items-center gap-1.5 text-slate-600">
            <span className="inline-block h-2.5 w-2.5 rounded-full bg-emerald-500" />
            {t('chart.revenue', 'Revenue')}
          </span>
          <span className="flex items-center gap-1.5 text-slate-600">
            <span className="inline-block h-2.5 w-2.5 rounded-full bg-rose-400" />
            {t('chart.expenses', 'Expenses')}
          </span>
        </div>
      </div>

      {loading ? (
        <div className="flex h-64 items-center justify-center text-sm text-slate-400">
          {t('chart.loading', 'Loading chart…')}
        </div>
      ) : (
        <svg viewBox={`0 0 ${W} ${H}`} className="mt-4 w-full" role="img" aria-label={t('chart.title', 'Revenue vs Expenses')}>
          <defs>
            <linearGradient id="revFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#10b981" stopOpacity="0.35" />
              <stop offset="100%" stopColor="#10b981" stopOpacity="0.02" />
            </linearGradient>
            <linearGradient id="expFill" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#fb7185" stopOpacity="0.30" />
              <stop offset="100%" stopColor="#fb7185" stopOpacity="0.02" />
            </linearGradient>
          </defs>

          {ticks.map((tick, i) => (
            <g key={i}>
              <line
                x1={padL}
                x2={W - padR}
                y1={y(tick)}
                y2={y(tick)}
                stroke="#e2e8f0"
                strokeDasharray="3 3"
              />
              <text x={padL - 8} y={y(tick) + 4} textAnchor="end" fontSize="10" fill="#94a3b8">
                {tick >= 1000 ? `${(tick / 1000).toFixed(1)}k` : tick.toFixed(0)}
              </text>
            </g>
          ))}

          {points.length > 0 && (
            <>
              <path d={`${line((p) => p.expenses)} L${x(points.length - 1).toFixed(1)},${(H - padB).toFixed(1)} L${x(0).toFixed(1)},${(H - padB).toFixed(1)} Z`} fill="url(#expFill)" />
              <path d={`${line((p) => p.revenue)} L${x(points.length - 1).toFixed(1)},${(H - padB).toFixed(1)} L${x(0).toFixed(1)},${(H - padB).toFixed(1)} Z`} fill="url(#revFill)" />
              <path d={line((p) => p.expenses)} fill="none" stroke="#fb7185" strokeWidth="2" />
              <path d={line((p) => p.revenue)} fill="none" stroke="#10b981" strokeWidth="2.5" />
              {points.map((p, i) => (
                <g key={i}>
                  <circle cx={x(i)} cy={y(p.revenue)} r="3.5" fill="#10b981" stroke="#fff" strokeWidth="1.5" />
                  <text x={x(i)} y={H - 10} textAnchor="middle" fontSize="10" fill="#64748b">
                    {p.label}
                  </text>
                </g>
              ))}
            </>
          )}
        </svg>
      )}
    </div>
  )
}
