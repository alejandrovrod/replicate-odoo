import i18n from './i18n'

/**
 * Display locale for numbers and money (spec 00-i18n: regional formats follow the UI language
 * through `Intl`, never a hardcoded locale). Read at call time from the i18next singleton so a
 * language switch takes effect on the next render without threading a locale through every
 * call site. Unknown tags fall back to `en-US`.
 */
export const displayLocale = (): string => {
  const tag = (i18n.resolvedLanguage ?? i18n.language ?? 'en').toLowerCase()
  if (tag.startsWith('es')) return 'es-ES'
  return 'en-US'
}

/** Quantity display: thousands separators, at most two decimals, no trailing zeros. */
export const formatQty = (qty: number): string =>
  qty.toLocaleString(displayLocale(), { maximumFractionDigits: 2 })

/** Money display (USD in the dev seed - the currency, unlike the locale, is data). */
export const formatMoney = (value: number): string =>
  value.toLocaleString(displayLocale(), { style: 'currency', currency: 'USD' })

/**
 * Delta display: 2 decimals for normal amounts but up to 4, because a difference just past a
 * tight tolerance would otherwise render as a red badge reading "$0.00".
 */
export const formatDelta = (value: number): string =>
  value.toLocaleString(displayLocale(), {
    style: 'currency',
    currency: 'USD',
    minimumFractionDigits: 2,
    maximumFractionDigits: 4,
  })
