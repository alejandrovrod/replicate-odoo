// Re-exported from the shared formatter so existing stock imports keep working;
// `localISODate` stays here because it is a wire format (`DateOnly`), not a display format.
export { displayLocale, formatMoney, formatQty } from '../../lib/format'

/**
 * Local `yyyy-mm-dd`, the `DateOnly` format the API expects for `postingDate`.
 * Built from local parts on purpose: `toISOString()` would shift the day around UTC midnight.
 */
export function localISODate(date = new Date()): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}
