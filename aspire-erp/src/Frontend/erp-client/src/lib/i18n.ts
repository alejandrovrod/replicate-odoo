import i18n from 'i18next'
import Backend from 'i18next-http-backend'
import LanguageDetector from 'i18next-browser-languagedetector'
import { initReactI18next } from 'react-i18next'

/**
 * Single source of truth for UI language (spec 00-i18n, task F-02).
 *
 * - `en` is the default and the fallback: a key missing from `es` resolves to English instead
 *   of leaking the raw key into the UI (same rule the backend applies to its resx catalog).
 * - Translations are fetched on demand from `/locales/{lng}/{ns}.json` so each feature namespace
 *   stays a separate chunk (Phase 3 lazy-loading acceptance).
 * - The chosen language is cached in `localStorage` under `aspire-erp-lang`, the same key
 *   `src/api/client.ts` reads when it sends `Accept-Language` - one key, both sides of the wire.
 */
export const LANGUAGE_STORAGE_KEY = 'aspire-erp-lang'

export const SUPPORTED_LANGUAGES = ['en', 'es'] as const
export type SupportedLanguage = (typeof SUPPORTED_LANGUAGES)[number]

export const DEFAULT_LANGUAGE: SupportedLanguage = 'en'

/** Every namespace shipped by `public/locales`. Mirrored by `src/types/i18n.d.ts`. */
export const NAMESPACES = [
  'common',
  'error',
  'crm',
  'stock',
  'selling',
  'buying',
  'manufacturing',
  'banking',
  'accounting',
  'hr-payroll',
  'assets',
  'dashboard',
] as const

/** Narrows any detected tag (`es-AR`, `en-US`, ...) to a language we actually ship. */
export function normalizeLanguage(lng: string | null | undefined): SupportedLanguage {
  if (!lng) return DEFAULT_LANGUAGE
  const base = lng.toLowerCase().split('-')[0]
  return (SUPPORTED_LANGUAGES as readonly string[]).includes(base)
    ? (base as SupportedLanguage)
    : DEFAULT_LANGUAGE
}

void i18n
  .use(Backend)
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    fallbackLng: DEFAULT_LANGUAGE,
    supportedLngs: SUPPORTED_LANGUAGES,
    nonExplicitSupportedLngs: true,
    defaultNS: 'common',
    // Only the cross-cutting namespaces boot with the app: every feature namespace loads on
    // demand the first time a component calls `useTranslation('<ns>')` (spec 00-i18n lazy-loading
    // acceptance). Listing all of NAMESPACES here would defeat that and fetch every JSON
    // upfront — verified once via the e2e traffic capture, do not "simplify" this back.
    ns: ['common', 'error'],
    partialBundledLanguages: true,

    backend: {
      loadPath: '/locales/{{lng}}/{{ns}}.json',
    },

    detection: {
      order: ['localStorage', 'navigator'],
      lookupLocalStorage: LANGUAGE_STORAGE_KEY,
      caches: ['localStorage'],
      convertDetectedLanguage: (lng: string) => normalizeLanguage(lng),
    },

    interpolation: { escapeValue: false },
    returnNull: false,

    react: {
      useSuspense: false,
    },
  })

export default i18n
