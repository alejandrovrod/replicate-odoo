import { useTranslation } from 'react-i18next'
import { Languages } from 'lucide-react'
import { SUPPORTED_LANGUAGES, type SupportedLanguage, normalizeLanguage } from '../../lib/i18n'

const LANGUAGE_LABELS: Record<SupportedLanguage, string> = {
  en: 'English',
  es: 'Español',
}

/**
 * Language switch for the header (spec 00-i18n, task F-08).
 *
 * `i18n.changeLanguage` is what actually moves the UI, and i18next's detector is configured to
 * cache the choice in `localStorage` under `aspire-erp-lang` - the very key `src/api/client.ts`
 * reads for `Accept-Language`. One write, both the rendered text and the wire follow it.
 */
export function LanguageSelector() {
  const { t, i18n } = useTranslation('common')

  const current = normalizeLanguage(i18n.resolvedLanguage ?? i18n.language)

  const handleChange = (event: React.ChangeEvent<HTMLSelectElement>) => {
    void i18n.changeLanguage(normalizeLanguage(event.target.value))
  }

  return (
    <div className="flex items-center gap-1.5 rounded-lg border border-slate-200 bg-slate-50 px-2 py-1.5">
      <Languages className="h-3.5 w-3.5 text-slate-400" aria-hidden="true" />
      <label className="sr-only" htmlFor="erp-language-select">
        {t('header.language')}
      </label>
      <select
        id="erp-language-select"
        value={current}
        onChange={handleChange}
        data-testid="language-select"
        className="cursor-pointer bg-transparent pr-1 text-xs font-medium text-slate-700 outline-none"
      >
        {SUPPORTED_LANGUAGES.map((lng) => (
          <option key={lng} value={lng}>
            {LANGUAGE_LABELS[lng]}
          </option>
        ))}
      </select>
    </div>
  )
}
