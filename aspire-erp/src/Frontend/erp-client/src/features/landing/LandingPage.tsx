import { useTranslation } from 'react-i18next'
import { ArrowRight, Flame } from 'lucide-react'
import { LandingNavbar } from './LandingNavbar'
import { HeroSection } from './HeroSection'
import { ChameleonShowcase } from './ChameleonShowcase'
import { ModulesBentoGrid } from './ModulesBentoGrid'
import { TechStackSection } from './TechStackSection'
import { LandingFooter } from './LandingFooter'

interface LandingPageProps {
  onLoginClick: () => void
  onDirectDemoClick: () => void
}

export function LandingPage({ onLoginClick, onDirectDemoClick }: LandingPageProps) {
  const { t } = useTranslation('landing')

  const scrollToSection = (id: string) => {
    const el = document.getElementById(id)
    if (el) {
      el.scrollIntoView({ behavior: 'smooth' })
    }
  }

  return (
    <div className="min-h-screen bg-white text-slate-900 selection:bg-slate-900 selection:text-white">
      {/* Floating Capsule Navbar inspired by Shadcnblocks Mainline */}
      <LandingNavbar onLoginClick={onLoginClick} />

      <main>
        {/* Hero Section with Mainline Curved Container & Side Features */}
        <HeroSection
          onExploreClick={() => scrollToSection('chameleon')}
          onDemoClick={onDirectDemoClick}
        />

        {/* Chameleon Architecture Showcase */}
        <ChameleonShowcase />

        {/* Enterprise Modules Bento Grid */}
        <ModulesBentoGrid />

        {/* Technical Architecture & Engineering Stack */}
        <TechStackSection />

        {/* Mainline Style Pre-Footer CTA */}
        <section className="py-24 bg-white border-t border-slate-200">
          <div className="mx-auto max-w-5xl px-4 text-center sm:px-6 lg:px-8">
            <div className="mx-3 sm:mx-6 rounded-3xl border border-blue-100 bg-gradient-to-b from-blue-50/70 via-slate-50 to-white p-10 sm:p-16 text-slate-900 shadow-sm">
              <div className="inline-flex items-center gap-2 rounded-full border border-blue-200 bg-white px-3 py-1 text-xs font-mono font-medium text-blue-700 mb-6 shadow-2xs">
                <Flame className="h-3.5 w-3.5 text-blue-600" />
                <span>{t('cta.envBadge')}</span>
              </div>

              <h2 className="font-heading text-3xl font-extrabold tracking-tight sm:text-5xl text-slate-900">
                {t('cta.title')}
              </h2>

              <p className="mx-auto mt-4 max-w-xl text-sm sm:text-base text-slate-600 leading-relaxed">
                {t('cta.subtitle')}
              </p>

              <div className="mt-8 flex flex-col items-center justify-center gap-3 sm:flex-row">
                <button
                  type="button"
                  onClick={onDirectDemoClick}
                  className="inline-flex items-center justify-center gap-2 rounded-xl bg-blue-50 border border-blue-200 px-6 py-3.5 text-xs sm:text-sm font-bold text-blue-700 shadow-2xs hover:bg-blue-100 transition"
                >
                  <span>{t('cta.button')}</span>
                  <ArrowRight className="h-4 w-4 text-blue-500" />
                </button>

                <button
                  type="button"
                  onClick={onLoginClick}
                  className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-6 py-3.5 text-xs sm:text-sm font-semibold text-slate-700 shadow-2xs hover:bg-slate-50 hover:text-slate-900 transition"
                >
                  <span>{t('cta.login')}</span>
                </button>
              </div>
            </div>
          </div>
        </section>
      </main>

      {/* Footer */}
      <LandingFooter />
    </div>
  )
}
