import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Sparkles,
  Stethoscope,
  Wrench,
  HardHat,
  Truck,
  CheckCircle2,
  Users,
  FileText,
  PackageCheck,
  Receipt,
  ClipboardCheck,
} from 'lucide-react'

type VerticalTab = 'healthcare' | 'automotive' | 'construction' | 'distribution'

export function ChameleonShowcase() {
  const { t } = useTranslation('landing')
  const [activeTab, setActiveTab] = useState<VerticalTab>('healthcare')

  const tabIcons = {
    healthcare: Stethoscope,
    automotive: Wrench,
    construction: HardHat,
    distribution: Truck,
  }

  const ActiveIcon = tabIcons[activeTab]

  return (
    <section id="industries" className="relative scroll-mt-20 py-24">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        
        {/* Mainline style dashed section divider with central label */}
        <div className="relative flex items-center justify-center mb-16">
          <div className="relative h-px w-full text-amber-300/60">
            <div className="h-px w-full dashed-line-x" />
          </div>
          <span className="absolute bg-[#faf8f5] px-4 font-mono text-xs font-semibold uppercase tracking-wider text-slate-500">
            {t('chameleon.dividerLabel')}
          </span>
        </div>

        {/* Section Heading */}
        <div className="mx-auto max-w-3xl text-center">
          <div className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-slate-50 px-3 py-1 text-xs font-mono font-medium text-slate-700 mb-4">
            <Sparkles className="h-3.5 w-3.5 text-amber-500" />
            <span>{t('chameleon.badge')}</span>
          </div>

          <h2 className="font-heading text-3xl font-extrabold tracking-tight text-slate-950 sm:text-4xl lg:text-5xl">
            {t('chameleon.title')}
          </h2>
          <p className="mt-4 text-sm sm:text-base text-slate-600 leading-relaxed">
            {t('chameleon.subtitle')}
          </p>
        </div>

        {/* Shadcn style Pill Tabs */}
        <div className="mt-10 flex flex-wrap justify-center gap-2">
          {(['healthcare', 'automotive', 'construction', 'distribution'] as const).map((tabKey) => {
            const Icon = tabIcons[tabKey]
            const isSelected = activeTab === tabKey
            return (
              <button
                key={tabKey}
                type="button"
                onClick={() => setActiveTab(tabKey)}
                className={`inline-flex items-center gap-2 rounded-full px-4 py-2 text-xs font-semibold transition-all ${
                  isSelected
                    ? 'border border-blue-300 bg-blue-50 text-blue-700 shadow-2xs font-bold'
                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200 hover:text-slate-900'
                }`}
              >
                <Icon className="h-3.5 w-3.5" />
                <span>{t(`chameleon.tabs.${tabKey}` as any)}</span>
              </button>
            )
          })}
        </div>

        {/* Mainline Card Layout with subtle borders and dashed lines */}
        <div className="mt-12 overflow-hidden rounded-3xl border border-amber-200/60 bg-white/85 shadow-sm backdrop-blur-md">
          <div className="grid grid-cols-1 lg:grid-cols-12">
            
            {/* Left: Terminology Mapping */}
            <div className="p-8 sm:p-12 lg:col-span-5 space-y-6">
              <div className="flex items-center gap-2">
                <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-slate-100 text-slate-800">
                  <ActiveIcon className="h-4 w-4" />
                </span>
                <h3 className="font-heading text-xl font-bold text-slate-950">
                  {t(`chameleon.${activeTab}.name` as any)}
                </h3>
              </div>

              <p className="text-xs sm:text-sm text-slate-600 leading-relaxed">
                {t(`chameleon.${activeTab}.description` as any)}
              </p>

              {/* Lexical Mapping Grid */}
              <div className="grid grid-cols-2 gap-3 pt-2">
                <div className="rounded-xl border border-slate-100 bg-slate-50/70 p-3.5">
                  <div className="flex items-center gap-1.5 text-[10px] font-mono text-slate-400 uppercase">
                    <Users className="h-3 w-3" />
                    <span>{t('chameleon.colClient')}</span>
                  </div>
                  <div className="mt-1 text-xs font-bold text-slate-900">
                    {t(`chameleon.${activeTab}.customerTerm` as any)}
                  </div>
                </div>

                <div className="rounded-xl border border-slate-100 bg-slate-50/70 p-3.5">
                  <div className="flex items-center gap-1.5 text-[10px] font-mono text-slate-400 uppercase">
                    <FileText className="h-3 w-3" />
                    <span>{t('chameleon.colOrder')}</span>
                  </div>
                  <div className="mt-1 text-xs font-bold text-slate-900">
                    {t(`chameleon.${activeTab}.orderTerm` as any)}
                  </div>
                </div>

                <div className="rounded-xl border border-slate-100 bg-slate-50/70 p-3.5">
                  <div className="flex items-center gap-1.5 text-[10px] font-mono text-slate-400 uppercase">
                    <PackageCheck className="h-3 w-3" />
                    <span>{t('chameleon.colDelivery')}</span>
                  </div>
                  <div className="mt-1 text-xs font-bold text-slate-900">
                    {t(`chameleon.${activeTab}.deliveryTerm` as any)}
                  </div>
                </div>

                <div className="rounded-xl border border-slate-100 bg-slate-50/70 p-3.5">
                  <div className="flex items-center gap-1.5 text-[10px] font-mono text-slate-400 uppercase">
                    <Receipt className="h-3 w-3" />
                    <span>{t('chameleon.colInvoice')}</span>
                  </div>
                  <div className="mt-1 text-xs font-bold text-slate-900">
                    {t(`chameleon.${activeTab}.invoiceTerm` as any)}
                  </div>
                </div>
              </div>

              {/* Feature Points */}
              <div className="space-y-2 pt-2">
                {[1, 2, 3].map((num) => (
                  <div key={num} className="flex items-start gap-2 text-xs text-slate-700">
                    <CheckCircle2 className="h-4 w-4 shrink-0 text-slate-900 mt-0.5" />
                    <span>{t(`chameleon.${activeTab}.feature${num}` as any)}</span>
                  </div>
                ))}
              </div>
            </div>

            {/* Right: Operations & Audit Panel */}
            <div className="relative border-t lg:border-t-0 lg:border-l border-slate-200 bg-slate-50/50 p-8 sm:p-12 lg:col-span-7 flex flex-col justify-between">
              <div>
                <div className="flex items-center justify-between pb-4 border-b border-slate-200">
                  <div className="flex items-center gap-2">
                    <ClipboardCheck className="h-4 w-4 text-slate-700" />
                    <span className="font-mono text-xs font-bold text-slate-900">
                      {t('chameleon.auditTrailTitle')}
                    </span>
                  </div>
                  <span className="font-mono text-[10px] font-medium text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
                    {t('chameleon.presetActiveBadge')}
                  </span>
                </div>

                {/* Table representation */}
                <div className="mt-5 space-y-2.5 font-mono text-xs">
                  <div className="grid grid-cols-4 gap-2 rounded-lg bg-white px-3 py-2 text-[10px] font-bold text-slate-400 border border-slate-100">
                    <div>{t('chameleon.thDoc')}</div>
                    <div>{t(`chameleon.${activeTab}.customerTerm` as any).toUpperCase()}</div>
                    <div>{t('chameleon.thType')}</div>
                    <div className="text-right">{t('chameleon.thDebitCredit')}</div>
                  </div>

                  <div className="grid grid-cols-4 items-center gap-2 rounded-lg bg-white px-3 py-2.5 border border-slate-200/80 shadow-2xs">
                    <div className="font-semibold text-slate-900">INV-2026-081</div>
                    <div className="truncate text-slate-600">Sanatorio Central</div>
                    <div className="text-slate-500 text-[11px] truncate">{t(`chameleon.${activeTab}.orderTerm` as any)}</div>
                    <div className="text-right font-bold text-slate-950">$ 4,200.00</div>
                  </div>

                  <div className="grid grid-cols-4 items-center gap-2 rounded-lg bg-white px-3 py-2.5 border border-slate-200/80 shadow-2xs">
                    <div className="font-semibold text-slate-900">INV-2026-082</div>
                    <div className="truncate text-slate-600">Servicios Globales</div>
                    <div className="text-slate-500 text-[11px] truncate">{t(`chameleon.${activeTab}.invoiceTerm` as any)}</div>
                    <div className="text-right font-bold text-slate-950">$ 11,850.00</div>
                  </div>
                </div>
              </div>

              {/* Bottom Quote Banner */}
              <div className="mt-6 rounded-xl border border-slate-200 bg-white p-4 text-[11px] text-slate-600">
                <span className="font-bold text-slate-900 block mb-0.5">{t('chameleon.guaranteeTitle')}</span>
                {t('chameleon.guaranteeDesc')}
              </div>
            </div>

          </div>
        </div>

      </div>
    </section>
  )
}
