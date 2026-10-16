import { describe, expect, it } from 'vitest'
import { apportionLandedCost, roundAway } from './landedCostCalculation'

describe('apportionLandedCost (server-engine mirror)', () => {
  const lines = [
    { key: 'a', qty: 100, amount: 1000, originalRate: 10 },
    { key: 'b', qty: 50, amount: 1000, originalRate: 20 },
  ]

  it('matches LCV-G1 (by amount)', () => {
    const result = apportionLandedCost(lines, 200, 'Amount')

    expect(result[0]).toMatchObject({ applicableCharges: 100, newRate: 11 })
    expect(result[1]).toMatchObject({ applicableCharges: 100, newRate: 22 })
  })

  it('matches LCV-G2 (by quantity)', () => {
    const result = apportionLandedCost(lines, 300, 'Quantity')

    expect(result[0]).toMatchObject({ applicableCharges: 200, newRate: 12 })
    expect(result[1]).toMatchObject({ applicableCharges: 100, newRate: 22 })
  })

  it('absorbs cent residuals in the last line', () => {
    const result = apportionLandedCost(
      [
        { key: 'a', qty: 1, amount: 1, originalRate: 1 },
        { key: 'b', qty: 1, amount: 1, originalRate: 1 },
        { key: 'c', qty: 1, amount: 1, originalRate: 1 },
      ],
      100,
      'Amount',
    )

    const total = result.reduce((sum, r) => sum + r.applicableCharges, 0)
    expect(total).toBe(100)
    expect(result.map((r) => r.applicableCharges)).toEqual([33.33, 33.33, 33.34])
  })

  it('rounds half away from zero like the server', () => {
    expect(roundAway(2.5, 0)).toBe(3)
    expect(roundAway(2.345, 2)).toBe(2.35)
  })
})
