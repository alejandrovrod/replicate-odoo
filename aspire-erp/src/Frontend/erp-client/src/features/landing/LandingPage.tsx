import { useTranslation } from 'react-i18next'
import { ArrowRight, Flame } from 'lucide-react'
import { LandingNavbar } from './LandingNavbar'
import { HeroSection } from './HeroSection'
import { ChameleonShowcase } from './ChameleonShowcase'
import { ModulesBentoGrid } from './ModulesBentoGrid'
import { EnterpriseBenefitsSection } from './EnterpriseBenefitsSection'
import { LandingFooter } from './LandingFooter'

interface LandingPageProps {
  onLoginClick: () => void
  onRegisterClick: () => void
  onDirectDemoClick: () => void
}

export function LandingPage({ onLoginClick, onRegisterClick, onDirectDemoClick }: LandingPageProps) {
  const { t } = useTranslation('landing')

  return (
    <div className="relative min-h-screen overflow-hidden bg-[#faf8f5] text-slate-900 selection:bg-slate-900 selection:text-white">
      {/* Organic Warm Geometric Shapes (Radix UI Theme Playground Motif) */}
      <div className="pointer-events-none absolute inset-0 overflow-hidden">
        {/* Soft yellow-amber geometric backdrop patches */}
        <div className="absolute -left-32 -top-20 h-[640px] w-[640px] rounded-[180px] bg-[#fbf3db] opacity-70 blur-3xl transform -rotate-12" />
        <div className="absolute -right-28 top-32 h-[720px] w-[620px] rounded-[200px] bg-[#fbf3db] opacity-75 blur-3xl transform rotate-12" />
        <div className="absolute top-[38%] left-[-100px] h-[550px] w-[550px] rounded-[160px] bg-[#fef9eb] opacity-80 blur-2xl transform rotate-45" />
        <div className="absolute top-[60%] -right-20 h-[600px] w-[600px] rounded-[180px] bg-[#fbf3db] opacity-60 blur-3xl transform -rotate-6" />
        <div className="absolute bottom-[-100px] left-1/3 h-[500px] w-[700px] rounded-[160px] bg-[#fef9eb] opacity-90 blur-2xl transform rotate-6" />

        {/* Crisp polygonal shapes echoing Radix playground art */}
        <svg
          className="absolute inset-0 h-full w-full opacity-35"
          xmlns="http://www.w3.org/2000/svg"
          preserveAspectRatio="none"
          viewBox="0 0 1440 1200"
        >
          <polygon points="0,0 380,0 220,680 0,520" fill="#f8eed1" opacity="0.6" />
          <polygon points="980,0 1440,0 1440,780 1120,580" fill="#f8eed1" opacity="0.55" />
          <polygon points="1440,720 1440,1200 980,1200" fill="#f5e8be" opacity="0.4" />
          <polygon points="0,820 420,1200 0,1200" fill="#f5e8be" opacity="0.45" />
        </svg>
      </div>

      {/* Floating Capsule Navbar inspired by Shadcnblocks Mainline */}
      <LandingNavbar onLoginClick={onLoginClick} onRegisterClick={onRegisterClick} />

      <main className="relative z-10">
        {/* Hero Section with Mainline Curved Container & Side Features */}
        <HeroSection
          onGetStartedClick={onRegisterClick}
          onDemoClick={onDirectDemoClick}
        />

        {/* Industry adaptation showcase */}
        <ChameleonShowcase />

        {/* Enterprise Modules Bento Grid */}
        <ModulesBentoGrid />

        {/* Business outcomes and enterprise benefits */}
        <EnterpriseBenefitsSection />

        {/* Mainline Style Pre-Footer CTA */}
        <section className="py-24 border-t border-amber-200/50">
          <div className="mx-auto max-w-5xl px-4 text-center sm:px-6 lg:px-8">
            <div className="mx-3 sm:mx-6 rounded-3xl border border-amber-200/60 bg-gradient-to-b from-amber-50/70 via-white/80 to-white/95 p-10 sm:p-16 text-slate-900 shadow-sm backdrop-blur-xs">
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
                  onClick={onRegisterClick}
                  className="inline-flex items-center justify-center gap-2 rounded-xl bg-amber-500 px-6 py-3.5 text-xs sm:text-sm font-bold text-amber-950 shadow-sm hover:bg-amber-400 transition"
                >
                  <span>{t('cta.register')}</span>
                  <ArrowRight className="h-4 w-4" />
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
