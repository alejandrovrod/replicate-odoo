/**
 * Client-side mirror of the server tax engine (`ITaxEngineService`, tax-engine Fase 2):
 * transferred taxes (Add), fiscal retentions (Deduct) and compound cascades
 * (OnPreviousRow), with the same signed-amount semantics (Grand = Net + Σ signed).
 *
 * PREVIEW ONLY - the server recomputes authoritatively on save. Rounding matches the
 * server (half away from zero, 2 decimals for sales) so the preview agrees to the cent
 * in every non-pathological case.
 */

export type TaxChargeType = 'Add' | 'Deduct'
export type TaxCalculationBasis = 'OnNetTotal' | 'OnPreviousRow' | 'FixedAmount'

export interface TaxRowInput {
  rate: number
  chargeType: TaxChargeType
  basis: TaxCalculationBasis
  fixedAmount?: number
}

export interface TaxRowResult {
  base: number
  amount: number
  chargeType: TaxChargeType
  basis: TaxCalculationBasis
  rate: number
}

export interface TaxCalculation {
  rows: TaxRowResult[]
  addTotal: number
  deductTotal: number
  taxTotal: number
  grandTotal: number
}

/** Half away from zero (C# MidpointRounding.AwayFromZero parity). */
export function roundAway(value: number, decimals: number): number {
  const factor = 10 ** decimals
  return (Math.sign(value) * Math.round(Math.abs(value) * factor)) / factor
}

export function calculateTaxes(
  netBase: number,
  rows: readonly TaxRowInput[],
  decimals = 2,
): TaxCalculation {
  let running = netBase
  const computed: TaxRowResult[] = rows.map((row) => {
    let base: number
    let raw: number
    if (row.basis === 'FixedAmount') {
      base = 0
      raw = roundAway(row.fixedAmount ?? 0, decimals)
    } else {
      base = row.basis === 'OnPreviousRow' ? running : netBase
      raw = roundAway((base * (Number(row.rate) || 0)) / 100, decimals)
    }
    const amount = row.chargeType === 'Deduct' ? -raw : raw
    running += amount
    return { base, amount, chargeType: row.chargeType, basis: row.basis, rate: Number(row.rate) || 0 }
  })

  const addTotal = computed.filter((r) => r.chargeType === 'Add').reduce((s, r) => s + r.amount, 0)
  const deductTotal = computed
    .filter((r) => r.chargeType === 'Deduct')
    .reduce((s, r) => s + Math.abs(r.amount), 0)
  const taxTotal = computed.reduce((s, r) => s + r.amount, 0)

  return { rows: computed, addTotal, deductTotal, taxTotal, grandTotal: netBase + taxTotal }
}
