import type { OutstandingInvoice } from './api/usePaymentEntries'

/** One settlement slice the FIFO planner decided on: invoice id + amount to allocate. */
export interface FifoAllocationSlice {
  invoiceId: string
  allocatedAmount: number
}

/**
 * FIFO settlement plan (ui-spec r-12 D2 / PAY-UI-G1): settle the oldest due invoice first,
 * capped by its outstanding balance, and stop once the cash runs out. Kept pure and outside
 * the component so the ordering + capping rules are unit-testable without a rendered modal.
 *
 * Ties on the due date fall back to the human invoice number, so the plan is deterministic.
 * Cash left over after the last invoice stays unallocated (the caller turns it into an advance);
 * a non-positive amount yields no slices.
 */
export function planFifoAllocations(
  outstanding: readonly OutstandingInvoice[],
  paidAmount: number,
): FifoAllocationSlice[] {
  let remaining = Number(paidAmount) || 0
  if (remaining <= 0) return []

  const ordered = [...outstanding].sort(
    (a, b) => a.dueDate.localeCompare(b.dueDate) || a.number.localeCompare(b.number),
  )

  const plan: FifoAllocationSlice[] = []
  for (const invoice of ordered) {
    if (remaining <= 0) break
    const amount = Math.min(invoice.outstandingAmount, remaining)
    if (amount > 0) {
      plan.push({ invoiceId: invoice.id, allocatedAmount: amount })
      remaining -= amount
    }
  }

  return plan
}
