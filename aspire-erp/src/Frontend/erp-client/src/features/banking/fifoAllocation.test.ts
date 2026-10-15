import { describe, expect, it } from 'vitest'
import { planFifoAllocations } from './fifoAllocation'
import type { OutstandingInvoice } from './api/usePaymentEntries'

function invoice(overrides: Partial<OutstandingInvoice> & Pick<OutstandingInvoice, 'id'>): OutstandingInvoice {
  return {
    number: overrides.id,
    postingDate: '2026-01-01',
    dueDate: '2026-01-31',
    grandTotal: 0,
    outstandingAmount: 0,
    status: 'Unpaid',
    ...overrides,
  }
}

// ui-spec r-12 background: ACME Corp with INV-01 ($300, due Jan) and INV-02 ($200, due Feb).
const INV_01 = invoice({ id: 'inv-01', number: 'INV-01', dueDate: '2026-01-15', outstandingAmount: 300 })
const INV_02 = invoice({ id: 'inv-02', number: 'INV-02', dueDate: '2026-02-15', outstandingAmount: 200 })

describe('planFifoAllocations (PAY-UI-G1 / G2)', () => {
  it('settles the oldest invoice first and spills over (G1: $400 against $300 + $200)', () => {
    const plan = planFifoAllocations([INV_01, INV_02], 400)

    expect(plan).toEqual([
      { invoiceId: 'inv-01', allocatedAmount: 300 },
      { invoiceId: 'inv-02', allocatedAmount: 100 },
    ])
  })

  it('caps at the open total when the payment exceeds it, leaving an advance (G2: $600 vs $500)', () => {
    const plan = planFifoAllocations([INV_01, INV_02], 600)

    expect(plan).toEqual([
      { invoiceId: 'inv-01', allocatedAmount: 300 },
      { invoiceId: 'inv-02', allocatedAmount: 200 },
    ])
    const allocated = plan.reduce((sum, slice) => sum + slice.allocatedAmount, 0)
    expect(600 - allocated).toBe(100) // the unallocated advance the modal highlights
  })

  it('orders by due date even when the input arrives out of order', () => {
    const plan = planFifoAllocations([INV_02, INV_01], 400)

    expect(plan.map((slice) => slice.invoiceId)).toEqual(['inv-01', 'inv-02'])
  })

  it('breaks due-date ties by invoice number', () => {
    const later = invoice({ id: 'inv-b', number: 'INV-B', dueDate: '2026-03-01', outstandingAmount: 50 })
    const earlier = invoice({ id: 'inv-a', number: 'INV-A', dueDate: '2026-03-01', outstandingAmount: 50 })

    const plan = planFifoAllocations([later, earlier], 50)

    expect(plan).toEqual([{ invoiceId: 'inv-a', allocatedAmount: 50 }])
  })

  it('returns no slices for a non-positive amount', () => {
    expect(planFifoAllocations([INV_01, INV_02], 0)).toEqual([])
    expect(planFifoAllocations([INV_01, INV_02], -10)).toEqual([])
  })
})
