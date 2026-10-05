// Spec 00-i18n, task F-05 (implementation note: keys are generated, not imported - Vite
// forbids importing anything from `public/`, so `scripts/i18n-sync.mjs` derives the literal
// unions from `public/locales/en/*.json` and emits `i18n.generated.d.ts`).
//
// Two non-obvious requirements, both verified by breaking them and watching `tsc -b` go red:
//   1. `CustomTypeOptions` is declared by the `i18next` package. Augmenting `react-i18next`
//      compiles cleanly and silently checks NOTHING.
//   2. `strictKeyChecks` defaults to false, which selects `TFunctionNonStrict` - permissive
//      even with `resources` populated. It must be turned on explicitly.
//
// Together these are the compile-time half of task F-23: `t('missing.key')` fails the build.
// The other half (English always carries the key) is enforced by `i18n-sync.mjs --check`.
import type { GeneratedResources } from './i18n.generated'

declare module 'i18next' {
  interface CustomTypeOptions {
    defaultNS: 'common'
    strictKeyChecks: true
    returnNull: false
    resources: GeneratedResources
  }
}
