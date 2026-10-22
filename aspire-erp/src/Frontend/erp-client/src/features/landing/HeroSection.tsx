import { useTranslation } from 'react-i18next'
import {
  ArrowRight,
  CircleDot,
  ChartNoAxesColumn,
  ShieldCheck,
  Zap,
  Sparkles,
  Boxes,
  Workflow,
} from 'lucide-react'

interface HeroSectionProps {
  onGetStartedClick: () => void
  onDemoClick: () => void
}

export function HeroSection({ onGetStartedClick, onDemoClick }: HeroSectionProps) {
  const { t } = useTranslation('landing')

  return (
    <section className="relative pt-12 pb-20 lg:pt-16 lg:pb-28">
      {/* Outer Curved Container inspired by Mainline template */}
      <div className="relative mx-3 mt-2 sm:mx-6 rounded-3xl border border-slate-200/90 bg-gradient-to-b from-slate-50 via-slate-50/50 to-white p-6 sm:p-12 lg:p-16 shadow-xs">
        
        {/* Subtle grid background */}
        <div className="pointer-events-none absolute inset-0 -z-10 rounded-3xl [background-image:radial-gradient(#cbd5e1_1px,transparent_1px)] [background-size:24px_24px] opacity-40" />

        <div className="mx-auto max-w-7xl">
          <div className="grid grid-cols-1 gap-12 lg:grid-cols-12 lg:gap-8 items-center">
            
            {/* Left Column: Hero Manifesto */}
            <div className="lg:col-span-7">
              {/* Commercial value badge */}
              <div className="inline-flex items-center gap-2 rounded-full border border-slate-200 bg-white px-3 py-1 text-[11px] font-mono font-medium text-slate-700 shadow-2xs mb-6">
                <span className="flex h-1.5 w-1.5 rounded-full bg-emerald-500 animate-pulse" />
                <span>{t('hero.badge')}</span>
              </div>

              <h1 className="font-heading text-3xl font-extrabold tracking-tight text-slate-950 sm:text-5xl lg:text-6xl leading-[1.1]">
                {t('hero.headlineMain')}{' '}
                <span className="text-slate-500 font-bold block sm:inline">
                  {t('hero.headlineSub')}
                </span>
              </h1>

              <p className="mt-6 max-w-xl text-base text-slate-600 sm:text-lg leading-relaxed">
                {t('hero.description')}
              </p>

              {/* Action Buttons */}
              <div className="mt-8 flex flex-wrap items-center gap-3">
                <button
                  type="button"
                  onClick={onDemoClick}
                  className="inline-flex items-center justify-center gap-2 rounded-xl bg-blue-50 border border-blue-200 px-5 py-3 text-xs sm:text-sm font-semibold text-blue-700 shadow-2xs hover:bg-blue-100 transition"
                >
                  <span>{t('hero.ctaDemo')}</span>
                  <ArrowRight className="h-4 w-4 text-blue-500" />
                </button>

                <button
                  type="button"
                  onClick={onGetStartedClick}
                  className="inline-flex items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-5 py-3 text-xs sm:text-sm font-semibold text-slate-700 shadow-2xs hover:bg-slate-50 hover:text-slate-900 transition"
                >
                  <Sparkles className="h-4 w-4 text-amber-500" />
                  <span>{t('hero.ctaStart')}</span>
                </button>
              </div>

              {/* Value Proofs */}
              <div className="mt-10 flex flex-wrap items-center gap-6 text-xs text-slate-500 font-mono">
                <span className="flex items-center gap-1.5">
                  <ShieldCheck className="h-4 w-4 text-emerald-600" />
                  {t('hero.proofAccuracy')}
                </span>
                <span className="flex items-center gap-1.5">
                  <Zap className="h-4 w-4 text-blue-600" />
                  {t('hero.proofReporting')}
                </span>
                <span className="flex items-center gap-1.5">
                  <Boxes className="h-4 w-4 text-indigo-600" />
                  {t('hero.proofTraceability')}
                </span>
              </div>
            </div>

            {/* Right Column: Shadcnblocks Vertical Feature Strip */}
            <div className="lg:col-span-5 relative lg:pl-10">
              {/* Dashed vertical separator on large screens */}
              <div className="hidden lg:block absolute left-0 top-0 bottom-0 w-px text-slate-300">
                <div className="h-full w-px dashed-line-y opacity-70" />
              </div>

              <div className="space-y-6">
                <div className="flex gap-4">
                  <CircleDot className="mt-0.5 h-5 w-5 shrink-0 text-blue-600" />
                  <div>
                    <h2 className="font-heading text-sm font-bold text-slate-900">
                      {t('hero.feature1Title')}
                    </h2>
                    <p className="mt-1 text-xs text-slate-600 leading-relaxed">
                      {t('hero.feature1Desc')}
                    </p>
                  </div>
                </div>

                <div className="flex gap-4">
                  <ShieldCheck className="mt-0.5 h-5 w-5 shrink-0 text-blue-600" />
                  <div>
                    <h2 className="font-heading text-sm font-bold text-slate-900">
                      {t('hero.feature2Title')}
                    </h2>
                    <p className="mt-1 text-xs text-slate-600 leading-relaxed">
                      {t('hero.feature2Desc')}
                    </p>
                  </div>
                </div>

                <div className="flex gap-4">
                  <Workflow className="mt-0.5 h-5 w-5 shrink-0 text-blue-600" />
                  <div>
                    <h2 className="font-heading text-sm font-bold text-slate-900">
                      {t('hero.feature3Title')}
                    </h2>
                    <p className="mt-1 text-xs text-slate-600 leading-relaxed">
                      {t('hero.feature3Desc')}
                    </p>
                  </div>
                </div>

                <div className="flex gap-4">
                  <ChartNoAxesColumn className="mt-0.5 h-5 w-5 shrink-0 text-blue-600" />
                  <div>
                    <h2 className="font-heading text-sm font-bold text-slate-900">
                      {t('hero.feature4Title')}
                    </h2>
                    <p className="mt-1 text-xs text-slate-600 leading-relaxed">
                      {t('hero.feature4Desc')}
                    </p>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Bottom Cockpit Interface Preview Card - Crisp Light Theme */}
          <div className="mt-14 overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
            {/* Window Bar */}
            <div className="flex items-center justify-between border-b border-slate-100 bg-slate-50/80 px-4 py-3">
              <div className="flex items-center gap-2">
                <div className="h-2.5 w-2.5 rounded-full bg-slate-300" />
                <div className="h-2.5 w-2.5 rounded-full bg-slate-300" />
                <div className="h-2.5 w-2.5 rounded-full bg-slate-300" />
                <span className="ml-2 font-mono text-[11px] text-slate-500">
                  {t('hero.cockpitTitle')}
                </span>
              </div>
              <div className="flex items-center gap-2 font-mono text-[10px] text-emerald-700 bg-emerald-50 px-2 py-0.5 rounded-full border border-emerald-200">
                <span className="h-1.5 w-1.5 rounded-full bg-emerald-500" />
                <span>{t('hero.cockpitStatus')}</span>
              </div>
            </div>

            {/* Metric Bars in Light Neutral Theme */}
            <div className="grid grid-cols-2 gap-4 p-6 sm:grid-cols-4 font-mono text-xs bg-slate-50/40">
              <div className="border-r border-slate-200/80 pr-4 last:border-0">
                <div className="text-[10px] text-slate-500 font-semibold uppercase">{t('hero.metric1Label')}</div>
                <div className="mt-1 text-xl font-bold text-slate-900">{t('hero.metric1Value')}</div>
                <div className="text-[10px] text-emerald-600 font-medium">{t('hero.metric1Sub')}</div>
              </div>

              <div className="border-r border-slate-200/80 pr-4 last:border-0">
                <div className="text-[10px] text-slate-500 font-semibold uppercase">{t('hero.metric2Label')}</div>
                <div className="mt-1 text-xl font-bold text-slate-900">{t('hero.metric2Value')}</div>
                <div className="text-[10px] text-blue-600 font-medium">{t('hero.metric2Sub')}</div>
              </div>

              <div className="border-r border-slate-200/80 pr-4 last:border-0">
                <div className="text-[10px] text-slate-500 font-semibold uppercase">{t('hero.metric3Label')}</div>
                <div className="mt-1 text-xl font-bold text-slate-900">{t('hero.metric3Value')}</div>
                <div className="text-[10px] text-amber-600 font-medium">{t('hero.metric3Sub')}</div>
              </div>

              <div>
                <div className="text-[10px] text-slate-500 font-semibold uppercase">{t('hero.metric4Label')}</div>
                <div className="mt-1 text-xl font-bold text-slate-900">{t('hero.metric4Value')}</div>
                <div className="text-[10px] text-emerald-600 font-medium">{t('hero.metric4Sub')}</div>
              </div>
            </div>
          </div>

        </div>
      </div>
    </section>
  )
}
