import { useTranslation } from 'react-i18next'
import { Flame, Globe, ArrowRight, ShieldCheck } from 'lucide-react'
import { type SupportedLanguage } from '../../lib/i18n'

interface LandingNavbarProps {
  onLoginClick: () => void
}

export function LandingNavbar({ onLoginClick }: LandingNavbarProps) {
  const { t, i18n } = useTranslation('landing')

  const toggleLanguage = () => {
    const nextLang: SupportedLanguage = i18n.language.startsWith('es') ? 'en' : 'es'
    i18n.changeLanguage(nextLang)
  }

  return (
    <header className="sticky top-4 z-50 mx-auto w-[min(94%,1100px)]">
      <div className="flex h-14 items-center justify-between rounded-full border border-slate-200/80 bg-white/80 px-4 py-2 shadow-sm shadow-slate-200/50 backdrop-blur-xl sm:px-6">
        {/* Brand: ASPIRE ERP */}
        <a href="#landing" className="flex items-center gap-2.5 group">
          <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-blue-100 text-blue-600 shadow-xs transition group-hover:bg-blue-200">
            <Flame className="h-4 w-4" />
          </div>
          <div className="flex items-center gap-1.5 font-heading">
            <span className="text-sm font-bold tracking-tight text-slate-800">Aspire</span>
            <span className="rounded-md bg-blue-50 px-1.5 py-0.5 text-[10px] font-mono font-bold text-blue-600 border border-blue-200/50">
              ERP
            </span>
          </div>
        </a>

        {/* Center Nav Links in Shadcn Style */}
        <nav className="hidden items-center gap-1 md:flex">
          <a
            href="#chameleon"
            className="rounded-full px-3.5 py-1.5 text-xs font-medium text-slate-600 transition hover:bg-slate-100 hover:text-slate-900"
          >
            {t('nav.chameleon')}
          </a>
          <a
            href="#modules"
            className="rounded-full px-3.5 py-1.5 text-xs font-medium text-slate-600 transition hover:bg-slate-100 hover:text-slate-900"
          >
            {t('nav.modules')}
          </a>
          <a
            href="#architecture"
            className="rounded-full px-3.5 py-1.5 text-xs font-medium text-slate-600 transition hover:bg-slate-100 hover:text-slate-900"
          >
            {t('nav.architecture')}
          </a>
        </nav>

        {/* Right Action Buttons */}
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={toggleLanguage}
            className="inline-flex h-8 items-center gap-1 rounded-full border border-slate-200 bg-white px-2.5 text-[11px] font-medium text-slate-700 shadow-2xs hover:bg-slate-50 transition"
            title="Toggle language"
          >
            <Globe className="h-3 w-3 text-slate-400" />
            <span>{i18n.language.startsWith('es') ? 'ES' : 'EN'}</span>
          </button>

          <button
            type="button"
            onClick={onLoginClick}
            className="inline-flex h-8 items-center gap-1.5 rounded-full border border-blue-200 bg-blue-50 px-4 text-xs font-semibold text-blue-700 shadow-xs hover:bg-blue-100 hover:text-blue-800 transition"
          >
            <ShieldCheck className="h-3.5 w-3.5 text-blue-600" />
            <span>{t('nav.signIn')}</span>
            <ArrowRight className="h-3 w-3 text-blue-500" />
          </button>
        </div>
      </div>
    </header>
  )
}
