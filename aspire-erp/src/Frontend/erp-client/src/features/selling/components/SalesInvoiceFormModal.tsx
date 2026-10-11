import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../api/client'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { useCustomers } from '../api/useCustomers'
import { useItems } from '../../stock/useStockData'
import { useAccountTree } from '../../accounting/useAccountTree'
import type { AccountTreeNode } from '../../accounting/types'
import type { CreateSalesInvoicePayload, SalesInvoice } from '../api/useSalesInvoices'

interface LineDraft {
  itemId: string
  quantity: number
  rate: number
}

interface TaxDraft {
  accountId: string
  rate: number
}

interface SalesInvoiceFormModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: CreateSalesInvoicePayload) => Promise<void>
  companyId: string
  /** Loaded invoices of the company (module 18: ReturnAgainst eligibility, client-side). */
  invoices: SalesInvoice[]
}

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

function round2(value: number): number {
  return Math.round(value * 100) / 100
}

/** Flattens the COA tree to selectable leaf liability accounts (module 17 task item). */
function liabilityLeaves(nodes: AccountTreeNode[]): { id: string; label: string }[] {
  const out: { id: string; label: string }[] = []
  const walk = (list: AccountTreeNode[]) => {
    for (const node of list) {
      if (!node.isGroup && node.isActive && node.rootType === 'Liability') {
        out.push({ id: node.id, label: `${node.code} — ${node.name}` })
      }
      if (node.children.length > 0) walk(node.children)
    }
  }
  walk(nodes)
  return out.sort((a, b) => a.label.localeCompare(b.label))
}

const SUBMITTED = ['Unpaid', 'PartiallyPaid', 'Paid']

export function SalesInvoiceFormModal({ isOpen, onClose, onSave, companyId, invoices }: SalesInvoiceFormModalProps) {
  const { t, i18n } = useTranslation('selling')
  const [customerId, setCustomerId] = useState('')
  const [postingDate, setPostingDate] = useState(todayIso())
  const [lines, setLines] = useState<LineDraft[]>([{ itemId: '', quantity: 1, rate: 0 }])
  const [discountPercentage, setDiscountPercentage] = useState(0)
  const [discountAmount, setDiscountAmount] = useState(0)
  const [taxes, setTaxes] = useState<TaxDraft[]>([])
  const [isReturn, setIsReturn] = useState(false)
  const [returnAgainstId, setReturnAgainstId] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [errorCode, setErrorCode] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const customersQuery = useCustomers(companyId, 1, 100)
  const itemsQuery = useItems(companyId, 1, 100)
  const accountsQuery = useAccountTree(companyId)
  const taxAccounts = useMemo(() => liabilityLeaves(accountsQuery.nodes), [accountsQuery.nodes])

  useEffect(() => {
    if (isOpen) {
      setCustomerId('')
      setPostingDate(todayIso())
      setLines([{ itemId: '', quantity: 1, rate: 0 }])
      setDiscountPercentage(0)
      setDiscountAmount(0)
      setTaxes([])
      setIsReturn(false)
      setReturnAgainstId('')
      setError(null)
      setErrorCode(null)
    }
  }, [isOpen])

  const eligibleReturns = useMemo(
    () =>
      invoices.filter(
        (inv) => inv.customerId === customerId && !inv.isReturn && SUBMITTED.includes(inv.status),
      ),
    [invoices, customerId],
  )

  // Live totals preview mirrors the server equation: Grand = Net - Discount + Taxes.
  const preview = useMemo(() => {
    const net = lines.reduce((sum, l) => sum + (Number(l.quantity) || 0) * (Number(l.rate) || 0), 0)
    const discount = discountAmount > 0 ? discountAmount : round2((Math.abs(net) * discountPercentage) / 100)
    const base = net - discount
    const taxRows = taxes.map((row) => round2((base * (Number(row.rate) || 0)) / 100))
    const taxTotal = taxRows.reduce((sum, v) => sum + v, 0)
    return { net, discount, base, taxRows, taxTotal, grand: base + taxTotal }
  }, [lines, discountPercentage, discountAmount, taxes])

  const updateLine = (index: number, patch: Partial<LineDraft>) => {
    setLines((prev) => prev.map((l, i) => (i === index ? { ...l, ...patch } : l)))
  }

  const updateTax = (index: number, patch: Partial<TaxDraft>) => {
    setTaxes((prev) => prev.map((row, i) => (i === index ? { ...row, ...patch } : row)))
  }

  const toggleReturn = (checked: boolean) => {
    setIsReturn(checked)
    if (!checked) {
      setReturnAgainstId('')
      setLines((prev) => prev.map((l) => ({ ...l, quantity: Math.abs(l.quantity) || 1 })))
    } else {
      // Returns demand negative quantities (ERPNext validate_qty parity): flip on toggle so
      // the user never hand-types the first minus sign.
      setLines((prev) => prev.map((l) => ({ ...l, quantity: -(Math.abs(l.quantity) || 1) })))
    }
  }

  const copyOriginalLines = () => {
    const original = eligibleReturns.find((inv) => inv.id === returnAgainstId)
    if (!original) return
    setLines(
      original.items.map((item) => ({
        itemId: item.itemId,
        quantity: -Math.abs(item.quantity),
        rate: item.rate,
      })),
    )
  }

  const fail = (message: string) => {
    setError(message)
    setErrorCode(null)
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    setErrorCode(null)
    if (!customerId) {
      fail(t('invoiceForm.errorCustomer', 'Customer is required.'))
      return
    }
    const validLines = lines.filter((l) => l.itemId && l.quantity !== 0)
    if (validLines.length === 0) {
      fail(t('invoiceForm.errorLines', 'Add at least one line with item and non-zero quantity.'))
      return
    }
    if (isReturn) {
      if (validLines.some((l) => l.quantity >= 0)) {
        fail(t('invoiceForm.errorReturnSign', 'Credit-note lines must carry negative quantities.'))
        return
      }
      if (!returnAgainstId) {
        fail(t('invoiceForm.errorReturnAgainst', 'Select the original invoice being credited.'))
        return
      }
    } else if (validLines.some((l) => l.quantity <= 0)) {
      fail(t('invoiceForm.errorPositiveQty', 'Quantities must be greater than zero.'))
      return
    }
    for (const row of taxes) {
      if (!row.accountId) {
        fail(t('invoiceForm.errorTaxAccount', 'Every tax row must select a liability account.'))
        return
      }
      if (!(row.rate >= 0 && row.rate <= 100)) {
        fail(t('invoiceForm.errorTaxRate', 'Tax rates must be between 0 and 100.'))
        return
      }
    }
    if (discountPercentage < 0 || discountPercentage > 100 || discountAmount < 0) {
      fail(t('invoiceForm.errorDiscount', 'Discount is invalid (0-100% and non-negative amount).'))
      return
    }
    setIsSubmitting(true)
    try {
      await onSave({
        companyId,
        customerId,
        postingDate,
        items: validLines,
        discountPercentage,
        discountAmount,
        taxes,
        isReturn,
        returnAgainstId: isReturn ? returnAgainstId : null,
      })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setErrorCode(err.code ?? null)
        setError(translateErrorCode(i18n, err.code, err.message))
      } else if (err instanceof Error) {
        setError(err.message)
      } else {
        setError(t('invoiceForm.errorGeneric', 'An error occurred while saving.'))
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl">
        <DialogHeader>
          <DialogTitle>
            {isReturn
              ? t('invoiceForm.titleReturn', 'New Credit Note')
              : t('invoiceForm.titleNew', 'New Sales Invoice')}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col overflow-hidden">
          <div className="flex-1 space-y-4 overflow-y-auto pr-2">
            {error && <div className="text-sm text-red-600">{error}</div>}
            {errorCode && (
              <div className="font-mono text-[11px] text-slate-400">code: {errorCode}</div>
            )}

            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              <div className="space-y-2">
                <label className="text-sm font-medium">Customer *</label>
                <select
                  value={customerId}
                  onChange={(e) => {
                    setCustomerId(e.target.value)
                    setReturnAgainstId('')
                  }}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  required
                >
                  <option value="">-- Select --</option>
                  {customersQuery.items.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.code} — {c.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">Posting date *</label>
                <Input type="date" value={postingDate} onChange={(e) => setPostingDate(e.target.value)} required />
              </div>
            </div>

            {/* Module 18: credit-note mode + provenance */}
            <div className="space-y-3 rounded-lg border border-slate-200 bg-slate-50 p-3">
              <label className="flex cursor-pointer items-center gap-2 text-sm font-medium text-slate-800">
                <input
                  type="checkbox"
                  className="h-4 w-4 accent-sky-600"
                  checked={isReturn}
                  onChange={(e) => toggleReturn(e.target.checked)}
                />
                {t('invoiceForm.isReturn', 'Is Return (Credit Note)')}
              </label>
              {isReturn && (
                <div className="flex flex-col gap-2">
                  <div className="flex gap-2">
                    <select
                      value={returnAgainstId}
                      onChange={(e) => setReturnAgainstId(e.target.value)}
                      className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                    >
                      <option value="">
                        {t('invoiceForm.returnAgainstPlaceholder', '-- Original invoice --')}
                      </option>
                      {eligibleReturns.map((inv) => (
                        <option key={inv.id} value={inv.id}>
                          {inv.invoiceNumber} — {inv.grandTotal.toLocaleString()}
                        </option>
                      ))}
                    </select>
                    <Button type="button" variant="outline" size="sm" onClick={copyOriginalLines} disabled={!returnAgainstId}>
                      {t('invoiceForm.copyLines', 'Copy lines')}
                    </Button>
                  </div>
                  {customerId && eligibleReturns.length === 0 && (
                    <p className="text-xs text-slate-500">
                      {t('invoiceForm.noEligible', 'No submitted invoices for this customer yet.')}
                    </p>
                  )}
                </div>
              )}
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <label className="text-sm font-medium">Lines</label>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setLines((prev) => [...prev, { itemId: '', quantity: isReturn ? -1 : 1, rate: 0 }])
                  }
                >
                  Add line
                </Button>
              </div>
              {lines.map((line, i) => (
                <div key={i} className="grid grid-cols-12 gap-2">
                  <select
                    value={line.itemId}
                    onChange={(e) => updateLine(i, { itemId: e.target.value })}
                    className="col-span-6 flex h-10 rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  >
                    <option value="">-- Item --</option>
                    {itemsQuery.items.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.code} — {item.name}
                      </option>
                    ))}
                  </select>
                  <Input
                    type="number"
                    step="0.0001"
                    value={line.quantity}
                    onChange={(e) => updateLine(i, { quantity: Number(e.target.value) })}
                    placeholder="Qty"
                    className="col-span-3"
                  />
                  <Input
                    type="number"
                    min={0}
                    step="0.0001"
                    value={line.rate}
                    onChange={(e) => updateLine(i, { rate: Number(e.target.value) })}
                    placeholder="Rate"
                    className="col-span-3"
                  />
                </div>
              ))}
            </div>

            {/* Module 17: global discount (Net Total default) */}
            <div className="space-y-2 rounded-lg border border-slate-200 bg-slate-50 p-3">
              <label className="text-sm font-medium text-slate-800">
                {t('invoiceForm.discountTitle', 'Global Discount (on Net Total)')}
              </label>
              <div className="grid grid-cols-2 gap-2">
                <div className="space-y-1">
                  <span className="text-xs text-slate-500">
                    {t('invoiceForm.discountPct', 'Percent %')}
                  </span>
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    step="0.01"
                    value={discountPercentage}
                    onChange={(e) => setDiscountPercentage(Number(e.target.value))}
                  />
                </div>
                <div className="space-y-1">
                  <span className="text-xs text-slate-500">
                    {t('invoiceForm.discountAmt', 'Amount')}
                  </span>
                  <Input
                    type="number"
                    min={0}
                    step="0.01"
                    value={discountAmount}
                    onChange={(e) => setDiscountAmount(Number(e.target.value))}
                  />
                </div>
              </div>
            </div>

            {/* Module 17: Taxes and Charges grid (liability leaves only) */}
            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <label className="text-sm font-medium">
                  {t('invoiceForm.taxesTitle', 'Taxes and Charges')}
                </label>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => setTaxes((prev) => [...prev, { accountId: '', rate: 21 }])}
                >
                  {t('invoiceForm.addTax', 'Add tax')}
                </Button>
              </div>
              {taxes.length === 0 && (
                <p className="text-xs text-slate-400">
                  {t('invoiceForm.noTaxes', 'No taxes - the invoice posts revenue only.')}
                </p>
              )}
              {taxes.map((row, i) => (
                <div key={i} className="grid grid-cols-12 items-center gap-2">
                  <select
                    value={row.accountId}
                    onChange={(e) => updateTax(i, { accountId: e.target.value })}
                    className="col-span-7 flex h-10 rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  >
                    <option value="">
                      {t('invoiceForm.taxAccountPlaceholder', '-- Liability account --')}
                    </option>
                    {taxAccounts.map((a) => (
                      <option key={a.id} value={a.id}>
                        {a.label}
                      </option>
                    ))}
                  </select>
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    step="0.01"
                    value={row.rate}
                    onChange={(e) => updateTax(i, { rate: Number(e.target.value) })}
                    placeholder="%"
                    className="col-span-3"
                  />
                  <div className="col-span-2 flex items-center justify-between">
                    <span className="font-mono text-xs text-slate-600">
                      {preview.taxRows[i]?.toLocaleString() ?? '0'}
                    </span>
                    <button
                      type="button"
                      className="text-xs text-red-600 hover:underline"
                      onClick={() => setTaxes((prev) => prev.filter((_, j) => j !== i))}
                    >
                      ✕
                    </button>
                  </div>
                </div>
              ))}
            </div>

            {/* Totals preview (server recomputes authoritatively on save) */}
            <div className="space-y-1 rounded-lg border border-slate-200 bg-white p-3 font-mono text-xs">
              <div className="flex justify-between text-slate-600">
                <span>{t('invoiceForm.totalNet', 'Net Total')}</span>
                <span>{preview.net.toLocaleString()}</span>
              </div>
              <div className="flex justify-between text-slate-600">
                <span>{t('invoiceForm.totalDiscount', 'Discount')}</span>
                <span>−{preview.discount.toLocaleString()}</span>
              </div>
              <div className="flex justify-between text-slate-600">
                <span>{t('invoiceForm.totalTax', 'Taxes')}</span>
                <span>{preview.taxTotal.toLocaleString()}</span>
              </div>
              <div className="flex justify-between border-t border-slate-100 pt-1 text-sm font-bold text-slate-900">
                <span>{t('invoiceForm.totalGrand', 'Grand Total')}</span>
                <span>{preview.grand.toLocaleString()}</span>
              </div>
            </div>
          </div>

          <DialogFooter className="border-t border-slate-100 pt-4">
            <Button type="button" variant="outline" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? 'Saving…' : 'Save'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
