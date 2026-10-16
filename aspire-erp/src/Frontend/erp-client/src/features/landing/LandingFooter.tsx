import { useTranslation } from 'react-i18next'
import { Flame, ArrowUp } from 'lucide-react'

export function LandingFooter() {
  const { t } = useTranslation('landing')

  const scrollToTop = () => {
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  return (
    <footer className="border-t border-slate-200 bg-white py-12">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        <div className="flex flex-col items-center justify-between gap-6 sm:flex-row">
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-blue-100 text-blue-600 shadow-xs">
              <Flame className="h-4 w-4" />
            </div>
            <div className="flex items-center gap-1.5 font-heading">
              <span className="text-sm font-bold text-slate-950">Aspire</span>
              <span className="rounded-md bg-slate-100 px-1.5 py-0.5 text-[10px] font-mono font-bold text-slate-600">
                ERP
              </span>
            </div>
          </div>

          <p className="text-xs font-mono text-slate-500">
            &copy; {new Date().getFullYear()} Aspire ERP. Built with .NET 10 &amp; React 19. All rights reserved.
          </p>

          <button
            type="button"
            onClick={scrollToTop}
            className="inline-flex items-center gap-1.5 rounded-full border border-slate-200 bg-white px-3 py-1.5 text-xs font-medium text-slate-700 shadow-2xs hover:bg-slate-50 transition"
          >
            <span>{t('footer.backToTop', 'Volver arriba')}</span>
            <ArrowUp className="h-3 w-3" />
          </button>
        </div>
      </div>
    </footer>
  )
}
