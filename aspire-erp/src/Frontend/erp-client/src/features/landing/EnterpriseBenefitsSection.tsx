import { useTranslation } from 'react-i18next'
import { Landmark, Boxes, Workflow, ShieldCheck } from 'lucide-react'

const BENEFIT_KEYS = ['financial', 'inventory', 'automation', 'security'] as const

const BENEFIT_ICONS = {
  financial: Landmark,
  inventory: Boxes,
  automation: Workflow,
  security: ShieldCheck,
}

export function EnterpriseBenefitsSection() {
  const { t } = useTranslation('landing')

  return (
    <section id="benefits" className="scroll-mt-20 py-24 border-t border-amber-200/50">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-3xl text-center">
          <span className="font-mono text-xs font-semibold uppercase tracking-wider text-slate-500">
            {t('enterpriseBenefits.sectionHeader')}
          </span>

          <h2 className="mt-3 font-heading text-3xl font-extrabold tracking-tight text-slate-950 sm:text-4xl lg:text-5xl">
            {t('enterpriseBenefits.title')}
          </h2>

          <p className="mt-4 text-sm sm:text-base text-slate-600 leading-relaxed">
            {t('enterpriseBenefits.subtitle')}
          </p>
        </div>

        <div className="mt-16 grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {BENEFIT_KEYS.map((key) => {
            const Icon = BENEFIT_ICONS[key]
            return (
              <div
                key={key}
                className="flex flex-col justify-between rounded-3xl border border-amber-200/60 bg-white/80 p-7 shadow-xs backdrop-blur-sm transition hover:border-amber-300 hover:shadow-md"
              >
                <div>
                  <div className="flex h-11 w-11 items-center justify-center rounded-2xl border border-blue-100 bg-blue-50 text-blue-600">
                    <Icon className="h-5 w-5" />
                  </div>
                  <div className="mt-4 font-mono text-[10px] font-semibold uppercase text-slate-400">
                    {t(`enterpriseBenefits.${key}.tag` as any)}
                  </div>
                  <h3 className="mt-2 font-heading text-base font-bold text-slate-950">
                    {t(`enterpriseBenefits.${key}.title` as any)}
                  </h3>
                  <p className="mt-2 text-xs text-slate-600 leading-relaxed">
                    {t(`enterpriseBenefits.${key}.desc` as any)}
                  </p>
                </div>
              </div>
            )
          })}
        </div>

        <div className="mt-16 rounded-3xl border border-slate-200 bg-slate-50 p-8 text-center sm:p-10">
          <blockquote className="mx-auto max-w-3xl font-heading text-lg font-bold leading-relaxed text-slate-900 sm:text-xl">
            &ldquo;{t('enterpriseBenefits.quoteText')}&rdquo;
          </blockquote>
          <div className="mt-4 font-mono text-xs text-slate-500">
            {t('enterpriseBenefits.quoteLabel')}
          </div>
        </div>
      </div>
    </section>
  )
}
