import type { i18n as I18nInstance } from 'i18next'
import type { EnCommonKeys } from '../types/i18n.generated'

export const DOC_STATUS_NAMESPACE = 'common'

/**
 * Maps a domain workflow status (`Draft`, `Paid`, `PartiallyDelivered`, ...) onto the
 * shared `common:docStatus.*` catalog (spec 00-i18n, same contract as `translateErrorCode`).
 *
 * Statuses arrive as runtime data, so the key can never be proven at compile time - we ask
 * i18next whether `common` actually carries it and only then translate, otherwise the raw
 * status renders (a correct code instead of a raw key, and new statuses never blank the UI).
 */
export function translateDocStatus(
  i18n: I18nInstance,
  status: string | null | undefined,
): string {
  if (!status) return ''
  const key = `docStatus.${status}`
  if (!i18n.exists(key, { ns: DOC_STATUS_NAMESPACE })) return status
  return i18n.t(key as EnCommonKeys, { ns: DOC_STATUS_NAMESPACE }) as string
}
