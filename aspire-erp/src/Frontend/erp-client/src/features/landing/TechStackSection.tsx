import { useTranslation } from 'react-i18next'
import { Terminal, Database, Server, Layout, ArrowRight } from 'lucide-react'

export function TechStackSection() {
  const { t } = useTranslation('landing')

  return (
    <section id="architecture" className="scroll-mt-20 py-24 bg-white border-t border-slate-200">
      <div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
        
        {/* Section Heading */}
        <div className="mx-auto max-w-3xl text-center">
          <span className="font-mono text-xs font-semibold uppercase tracking-wider text-slate-500">
            {t('techStack.sectionHeader')}
          </span>

          <h2 className="mt-3 font-heading text-3xl font-extrabold tracking-tight text-slate-950 sm:text-4xl lg:text-5xl">
            {t('techStack.title')}
          </h2>

          <p className="mt-4 text-sm sm:text-base text-slate-600 leading-relaxed">
            {t('techStack.subtitle')}
          </p>
        </div>

        {/* 4 Cards Grid */}
        <div className="mt-16 grid grid-cols-1 gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {(['dotnet', 'sql', 'cqrs', 'frontend'] as const).map((key) => {
            const icons = { dotnet: Server, sql: Database, cqrs: Terminal, frontend: Layout }
            const Icon = icons[key]
            return (
              <div
                key={key}
                className="flex flex-col justify-between rounded-3xl border border-slate-200 bg-white p-7 shadow-xs hover:border-slate-300 transition"
              >
                <div>
                  <div className="flex h-11 w-11 items-center justify-center rounded-2xl bg-blue-50 text-blue-600 border border-blue-100">
                    <Icon className="h-5 w-5" />
                  </div>
                  <div className="mt-4 font-mono text-[10px] font-semibold uppercase text-slate-400">
                    {t(`techStack.${key}.tag` as any)}
                  </div>
                  <h3 className="mt-2 font-heading text-base font-bold text-slate-950">
                    {t(`techStack.${key}.title` as any)}
                  </h3>
                  <p className="mt-2 text-xs text-slate-600 leading-relaxed">
                    {t(`techStack.${key}.desc` as any)}
                  </p>
                </div>
              </div>
            )
          })}
        </div>

        {/* Shadcnblocks Mainline Quote Block */}
        <div className="mt-16 rounded-3xl border border-slate-200 bg-slate-50 p-8 sm:p-10 text-center">
          <blockquote className="font-heading text-lg sm:text-xl font-bold text-slate-900 max-w-3xl mx-auto leading-relaxed">
            &ldquo;{t('techStack.quoteText')}&rdquo;
          </blockquote>
          <div className="mt-4 font-mono text-xs text-slate-500">
            {t('techStack.quoteLabel')}
          </div>
        </div>

      </div>
    </section>
  )
}
