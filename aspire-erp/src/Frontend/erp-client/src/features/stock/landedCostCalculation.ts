/**
 * Client-side mirror of the server apportionment (`ILandedCostCalculationService`, Fase 4):
 * spreads a charge total over receipt lines by value (Amount) or quantity (Quantity).
 *
 * PREVIEW ONLY - the server apportions authoritatively at Draft creation. Same exact-cents
 * rule (last line absorbs the residual) and half-away-from-zero rounding, so the preview
 * agrees with the Draft response to the cent.
 */

export type LandedCostDistributeBy = 'Amount' | 'Quantity'

export interface ApportionmentInput {
  key: string
  qty: number
  amount: number
  originalRate: number
}

export interface ApportionmentResult extends ApportionmentInput {
  applicableCharges: number
  newRate: number
}

/** Half away from zero (C# MidpointRounding.AwayFromZero parity). */
export function roundAway(value: number, decimals: number): number {
  const factor = 10 ** decimals
  return (Math.sign(value) * Math.round(Math.abs(value) * factor)) / factor
}

export function apportionLandedCost<T extends ApportionmentInput>(
  lines: readonly T[],
  totalCharges: number,
  distributeBy: LandedCostDistributeBy,
): (T & { applicableCharges: number; newRate: number })[] {
  const weights = lines.map((line) => (distributeBy === 'Amount' ? line.amount : line.qty))
  const totalWeight = weights.reduce((sum, w) => sum + w, 0)

  let distributed = 0
  return lines.map((line, i) => {
    const share =
      i === lines.length - 1
        ? roundAway(totalCharges - distributed, 2)
        : roundAway((totalCharges * weights[i]) / (totalWeight === 0 ? 1 : totalWeight), 2)
    if (i !== lines.length - 1) distributed += share
    return {
      ...line,
      applicableCharges: share,
      newRate: line.qty === 0 ? line.originalRate : roundAway((line.amount + share) / line.qty, 6),
    }
  })
}
