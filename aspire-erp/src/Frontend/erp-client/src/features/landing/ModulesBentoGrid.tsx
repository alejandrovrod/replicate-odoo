import { useTranslation } from 'react-i18next'
import {
  Scale,
  FileCheck2,
  Boxes,
  Factory,
  Building,
  ShieldCheck,
  ChevronRight,
} from 'lucide-react'

const MODULE_KEYS = ['accounting', 'selling', 'stock', 'manufacturing', 'assets', 'security'] as const

const MODULE_ICONS = {
  accounting: Scale,
  selling: FileCheck2,
  stock: Boxes,
  manufacturing: Factory,
  assets: Building,
  security: ShieldCheck,
}

export function ModulesBentoGrid() {
  const { t } = useTranslation('landing')

  return (
    <section id="modules" className="scroll-mt-20 py-24 bg-slate-50/60 border-t border-slate-200">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-3xl text-center">
          <span className="font-mono text-xs font-semibold uppercase tracking-wider text-slate-500">
            {t('modules.sectionHeader')}
          </span>

          <h2 className="mt-3 font-heading text-3xl font-extrabold tracking-tight text-slate-950 sm:text-4xl lg:text-5xl">
            {t('modules.title')}
          </h2>

          <p className="mt-4 text-sm sm:text-base text-slate-600 leading-relaxed">
            {t('modules.subtitle')}
          </p>
        </div>

        <div className="mt-16 grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-3">
          {MODULE_KEYS.map((key) => {
            const Icon = MODULE_ICONS[key]
            return (
              <div
                key={key}
                className="group flex flex-col justify-between rounded-3xl border border-amber-200/60 bg-white/80 p-7 shadow-xs backdrop-blur-sm transition-all duration-200 hover:border-amber-300 hover:shadow-md"
              >
                <div>
                  <div className="flex items-center justify-between">
                    <div className="flex h-11 w-11 items-center justify-center rounded-2xl border border-blue-100 bg-blue-50 text-blue-600 transition-colors group-hover:bg-blue-100">
                      <Icon className="h-5 w-5" />
                    </div>
                    <span className="rounded-full border border-slate-200 bg-slate-50 px-2.5 py-0.5 font-mono text-[10px] font-semibold text-slate-600">
                      {t(`modules.${key}.badge` as any)}
                    </span>
                  </div>

                  <h3 className="mt-5 font-heading text-lg font-bold text-slate-950">
                    {t(`modules.${key}.title` as any)}
                  </h3>

                  <p className="mt-2 text-xs sm:text-sm text-slate-600 leading-relaxed">
                    {t(`modules.${key}.description` as any)}
                  </p>
                </div>

                <div className="mt-6 flex items-center justify-between border-t border-slate-100 pt-4 text-xs">
                  <span className="font-mono text-[11px] text-slate-400">
                    {t(`modules.${key}.tag` as any)}
                  </span>
                  <div className="flex items-center gap-1 font-semibold text-slate-900 transition-transform group-hover:translate-x-0.5">
                    <span>{t('modules.viewDetail')}</span>
                    <ChevronRight className="h-3.5 w-3.5" />
                  </div>
                </div>
              </div>
            )
          })}
        </div>
      </div>
    </section>
  )
}
