import { describe, expect, it } from 'vitest'
import { calculateTaxes, roundAway } from './taxCalculation'

describe('calculateTaxes (server-engine mirror)', () => {
  it('matches TAX-G1 (retentions)', () => {
    const calc = calculateTaxes(
      10000,
      [
        { rate: 16, chargeType: 'Add', basis: 'OnNetTotal' },
        { rate: 10, chargeType: 'Deduct', basis: 'OnNetTotal' },
        { rate: 10.667, chargeType: 'Deduct', basis: 'OnNetTotal' },
      ],
      2,
    )

    expect(calc.rows.map((r) => r.amount)).toEqual([1600, -1000, -1066.7])
    expect(calc.grandTotal).toBe(9533.3)
  })

  it('matches TAX-G3 (cascade)', () => {
    const calc = calculateTaxes(
      1000,
      [
        { rate: 8, chargeType: 'Add', basis: 'OnNetTotal' },
        { rate: 16, chargeType: 'Add', basis: 'OnPreviousRow' },
      ],
      2,
    )

    expect(calc.rows[0]).toMatchObject({ base: 1000, amount: 80 })
    expect(calc.rows[1]).toMatchObject({ base: 1080, amount: 172.8 })
    expect(calc.grandTotal).toBe(1252.8)
  })

  it('books fixed amounts verbatim with charge sign', () => {
    const calc = calculateTaxes(
      1000,
      [{ rate: 0, chargeType: 'Deduct', basis: 'FixedAmount', fixedAmount: 250 }],
      2,
    )

    expect(calc.rows[0]).toMatchObject({ base: 0, amount: -250 })
    expect(calc.grandTotal).toBe(750)
  })

  it('rounds half away from zero like the server', () => {
    expect(roundAway(2.5, 0)).toBe(3)
    expect(roundAway(-2.5, 0)).toBe(-3)
    expect(roundAway(1066.705, 2)).toBe(1066.71)
  })
})
