/** Quantity display: thousands separators, at most two decimals, no trailing zeros. */
export const formatQty = (qty: number): string =>
  qty.toLocaleString('en-US', { maximumFractionDigits: 2 })

/** Money display for stock valuations (USD in the dev seed). */
export const formatMoney = (value: number): string =>
  value.toLocaleString('en-US', { style: 'currency', currency: 'USD' })

/**
 * Local `yyyy-mm-dd`, the `DateOnly` format the API expects for `postingDate`.
 * Built from local parts on purpose: `toISOString()` would shift the day around UTC midnight.
 */
export function localISODate(date = new Date()): string {
  const month = String(date.getMonth() + 1).padStart(2, '0')
  const day = String(date.getDate()).padStart(2, '0')
  return `${date.getFullYear()}-${month}-${day}`
}
