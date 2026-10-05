import type { i18n as I18nInstance } from 'i18next'
import type { EnErrorKeys } from '../types/i18n.generated'

export const ERROR_NAMESPACE = 'error'

/**
 * Maps an invariant `error.code` arriving from the wire onto the `error` namespace (spec 00-i18n,
 * task F-16).
 *
 * Codes are runtime data, so the key can never be proven at compile time - `strictKeyChecks`
 * would reject a template literal outright. Instead we ask i18next whether the `error` namespace
 * actually carries the code and only then translate, otherwise fall back to the backend's detail,
 * which is already localized through `Accept-Language`. `error.code` therefore stays the contract
 * and the UI degrades to a correct message rather than a raw key.
 */
export function translateErrorCode(
  i18n: I18nInstance,
  code: string | null | undefined,
  fallback?: string | null,
): string {
  if (!code) return fallback ?? ''
  if (!i18n.exists(code, { ns: ERROR_NAMESPACE })) return fallback ?? ''
  return i18n.t(code as EnErrorKeys, { ns: ERROR_NAMESPACE }) as string
}
