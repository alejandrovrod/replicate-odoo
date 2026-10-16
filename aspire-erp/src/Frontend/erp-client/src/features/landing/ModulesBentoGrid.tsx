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

export function ModulesBentoGrid() {
  const { t } = useTranslation('landing')

  const modules = [
    {
      key: 'accounting',
      icon: Scale,
      tag: 'GL & Treasury',
      badge: 'Doble Partida Inmutable',
      detail: 'DoubleEntryGuard con balance automático y auditoría temporal inmutable.',
    },
    {
      key: 'selling',
      icon: FileCheck2,
      tag: 'ERPNext Parity',
      badge: 'Direct Stock Update',
      detail: 'Facturación directa con rebaja de inventario y cruce de anticipos.',
    },
    {
      key: 'stock',
      icon: Boxes,
      tag: 'Inventory & Kardex',
      badge: 'FIFO Perpetual',
      detail: 'Costeo por lote/serie y prorrateo de fletes vía Landed Cost Vouchers.',
    },
    {
      key: 'manufacturing',
      icon: Factory,
      tag: 'Production',
      badge: 'BOM Multinivel',
      detail: 'Listas de materiales jerárquicas y cálculo de absorción de mano de obra.',
    },
    {
      key: 'assets',
      icon: Building,
      tag: 'Fixed Assets',
      badge: 'Depreciación Lineal',
      detail: 'Depreciación contable automatizada, custodia de activos y bajas.',
    },
    {
      key: 'security',
      icon: ShieldCheck,
      tag: 'Multi-Tenant',
      badge: '2FA Granular',
      detail: 'Aislamiento estricto de base de datos y permisos a nivel de DocType.',
    },
  ]

  return (
    <section id="modules" className="scroll-mt-20 py-24 bg-slate-50/60 border-t border-slate-200">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        
        {/* Section Header */}
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

        {/* 3-Column Card Grid with Shadcn rounded-3xl and button actions */}
        <div className="mt-16 grid grid-cols-1 gap-6 md:grid-cols-2 lg:grid-cols-3">
          {modules.map((m) => {
            const Icon = m.icon
            return (
              <div
                key={m.key}
                className="group flex flex-col justify-between rounded-3xl border border-slate-200 bg-white p-7 shadow-xs hover:border-slate-300 hover:shadow-md transition-all duration-200"
              >
                <div>
                  <div className="flex items-center justify-between">
                    <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-blue-50 text-blue-600 border border-blue-100 group-hover:bg-blue-100 transition-colors">
                      <Icon className="h-5 w-5" />
                    </div>
                    <span className="rounded-full border border-slate-200 bg-slate-50 px-2.5 py-0.5 font-mono text-[10px] font-semibold text-slate-600">
                      {m.badge}
                    </span>
                  </div>

                  <h3 className="mt-5 font-heading text-lg font-bold text-slate-950">
                    {t(`modules.${m.key}.title` as any)}
                  </h3>

                  <p className="mt-2 text-xs sm:text-sm text-slate-600 leading-relaxed">
                    {t(`modules.${m.key}.description` as any)}
                  </p>
                </div>

                <div className="mt-6 flex items-center justify-between border-t border-slate-100 pt-4 text-xs">
                  <span className="font-mono text-[11px] text-slate-400">{m.tag}</span>
                  <div className="flex items-center gap-1 font-semibold text-slate-900 group-hover:translate-x-0.5 transition-transform">
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
