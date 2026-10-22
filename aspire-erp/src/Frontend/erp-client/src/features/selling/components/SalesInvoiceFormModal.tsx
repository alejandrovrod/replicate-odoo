import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../api/client'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { useCustomers } from '../api/useCustomers'
import { useItems, useFlatWarehouses } from '../../stock/useStockData'
import type { EligibleAdvance } from '../api/useSalesInvoices'
import { useEligibleAdvances } from '../api/useSalesInvoices'
import { useAccountTree } from '../../accounting/useAccountTree'
import type { AccountTreeNode } from '../../accounting/types'
import type { CreateSalesInvoicePayload, SalesInvoice } from '../api/useSalesInvoices'
import { TaxBreakdownTable } from '../../accounting/components/TaxBreakdownTable'
import { TaxTemplateSelector } from '../../accounting/components/TaxTemplateSelector'
import type { TaxTemplate } from '../../accounting/api/useTaxTemplates'
import { calculateTaxes, type TaxCalculationBasis, type TaxChargeType } from '../../accounting/taxCalculation'

interface LineDraft {
  itemId: string
  quantity: number
  rate: number
}

interface TaxDraft {
  accountId: string
  rate: number
  chargeType: TaxChargeType
  basis: TaxCalculationBasis
  fixedAmount: number
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

/** Flattens the COA tree to selectable tax leaves (tax-engine Fase 2): Liability leaves
 * for transferred taxes plus Asset leaves for suffered retentions (booked as debit).
 * The server re-validates per-row charge rules authoritatively. */
function taxEligibleLeaves(nodes: AccountTreeNode[]): { id: string; label: string; rootType: string }[] {
  const out: { id: string; label: string; rootType: string }[] = []
  const walk = (list: AccountTreeNode[]) => {
    for (const node of list) {
      if (!node.isGroup && node.isActive && (node.rootType === 'Liability' || node.rootType === 'Asset')) {
        out.push({ id: node.id, label: `${node.code} — ${node.name}`, rootType: node.rootType })
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
  const [templateId, setTemplateId] = useState('')
  const [isReturn, setIsReturn] = useState(false)
  const [returnAgainstId, setReturnAgainstId] = useState('')
  const [updateStock, setUpdateStock] = useState(false)
  const [sourceWarehouseId, setSourceWarehouseId] = useState('')
  const [advanceRows, setAdvanceRows] = useState<{ paymentEntryId: string; amount: number }[]>([])
  const [error, setError] = useState<string | null>(null)
  const [errorCode, setErrorCode] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const customersQuery = useCustomers(companyId, 1, 100)
  const itemsQuery = useItems(companyId, 1, 100)
  const warehousesQuery = useFlatWarehouses(companyId, 1, 100, true)
  const eligibleQuery = useEligibleAdvances(companyId, customerId)
  const eligibleAdvances: EligibleAdvance[] = eligibleQuery.data ?? []
  const accountsQuery = useAccountTree(companyId)
  const taxAccounts = useMemo(() => taxEligibleLeaves(accountsQuery.nodes), [accountsQuery.nodes])
  const taxAccountLabel = useMemo(() => {
    const map = new Map(taxAccounts.map((a) => [a.id, a.label] as const))
    return (id: string) => map.get(id) ?? id
  }, [taxAccounts])

  useEffect(() => {
    if (isOpen) {
      setCustomerId('')
      setPostingDate(todayIso())
      setLines([{ itemId: '', quantity: 1, rate: 0 }])
      setDiscountPercentage(0)
      setDiscountAmount(0)
      setTaxes([])
      setTemplateId('')
      setIsReturn(false)
      setReturnAgainstId('')
      setUpdateStock(false)
      setSourceWarehouseId('')
      setAdvanceRows([])
      setError(null)
      setErrorCode(null)
    }
  }, [isOpen])

  // A new customer means new money: drop reservations against the previous one.
  useEffect(() => {
    setAdvanceRows([])
  }, [customerId])

  const eligibleReturns = useMemo(
    () =>
      invoices.filter(
        (inv) => inv.customerId === customerId && !inv.isReturn && SUBMITTED.includes(inv.status),
      ),
    [invoices, customerId],
  )

  // Live totals preview mirrors the server engine: Grand = Net - Discount + Σ signed
  // (Add follows the base, Deduct opposes it; cascades compound on the running total).
  const preview = useMemo(() => {
    const net = lines.reduce((sum, l) => sum + (Number(l.quantity) || 0) * (Number(l.rate) || 0), 0)
    const discount = discountAmount > 0 ? discountAmount : round2((Math.abs(net) * discountPercentage) / 100)
    const base = net - discount
    const calc = calculateTaxes(
      base,
      taxes.map((row) => ({
        rate: Number(row.rate) || 0,
        chargeType: row.chargeType,
        basis: row.basis,
        fixedAmount: Number(row.fixedAmount) || 0,
      })),
      2,
    )
    return { net, discount, base, calc, grand: calc.grandTotal }
  }, [lines, discountPercentage, discountAmount, taxes])

  /** Expands a template into editable rows (server recomputes authoritatively on save). */
  const applyTemplate = (template: TaxTemplate | null) => {
    setTemplateId(template ? template.id : '')
    if (!template) return
    setTaxes(
      template.lines.map((line) => ({
        accountId: line.accountId,
        rate: line.rate,
        chargeType: line.chargeType,
        basis: line.calculationBasis,
        fixedAmount: line.amount,
      })),
    )
  }

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
      // Returns demand negative quantities (standard quantity validation): flip on toggle so
      // the user never hand-types the first minus sign.
      setLines((prev) => prev.map((l) => ({ ...l, quantity: -(Math.abs(l.quantity) || 1) })))
      // Credit notes never carry advances (the server rejects them): drop the grid.
      setAdvanceRows([])
    }
  }

  /** Total reserved across the advance grid (client-side SI-G3 mirror of the server cap). */
  const allocatedTotal = advanceRows.reduce((sum, row) => sum + (Number(row.amount) || 0), 0)

  const toggleAdvance = (advance: EligibleAdvance) => {
    setAdvanceRows((prev) => {
      if (prev.some((row) => row.paymentEntryId === advance.paymentEntryId)) {
        return prev.filter((row) => row.paymentEntryId !== advance.paymentEntryId)
      }
      // Default to the full remainder; the user trims down for partial crosses.
      return [...prev, { paymentEntryId: advance.paymentEntryId, amount: advance.unallocatedAmount }]
    })
  }

  const updateAdvanceAmount = (paymentEntryId: string, amount: number) => {
    setAdvanceRows((prev) =>
      prev.map((row) => (row.paymentEntryId === paymentEntryId ? { ...row, amount } : row)),
    )
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
        fail(t('invoiceForm.errorTaxAccount', 'Every tax row must select a posting account (Liability for transfers, Liability/Asset for retentions).'))
        return
      }
      if (row.basis === 'FixedAmount') {
        if (!(Number(row.fixedAmount) >= 0)) {
          fail(t('invoiceForm.errorTaxFixed', 'Fixed-amount rows need a non-negative amount.'))
          return
        }
      } else if (!(row.rate >= 0 && row.rate <= 100)) {
        fail(t('invoiceForm.errorTaxRate', 'Tax rates must be between 0 and 100.'))
        return
      }
    }
    if (discountPercentage < 0 || discountPercentage > 100 || discountAmount < 0) {
      fail(t('invoiceForm.errorDiscount', 'Discount is invalid (0-100% and non-negative amount).'))
      return
    }
    if (!isReturn && updateStock && !sourceWarehouseId) {
      fail(t('invoiceForm.errorWarehouseRequired', 'Select the source warehouse for immediate delivery.'))
      return
    }
    if (!isReturn && advanceRows.length > 0) {
      for (const row of advanceRows) {
        const source = eligibleAdvances.find((a) => a.paymentEntryId === row.paymentEntryId)
        if (!source) {
          fail(t('invoiceForm.errorAdvanceStale', 'An advance is no longer available for this customer.'))
          return
        }
        if (!(row.amount > 0) || row.amount > source.unallocatedAmount) {
          fail(
            t('invoiceForm.errorAdvanceAmount', 'Advance amounts must be positive and within the payment remainder.'),
          )
          return
        }
      }
      if (allocatedTotal > Math.abs(preview.grand)) {
        fail(t('invoiceForm.errorAdvanceExceeds', 'Allocated advances cannot exceed the invoice total.'))
        return
      }
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
        taxes: taxes.map((row) => ({
          accountId: row.accountId,
          rate: Number(row.rate) || 0,
          chargeType: row.chargeType,
          calculationBasis: row.basis,
          fixedAmount: Number(row.fixedAmount) || 0,
        })),
        isReturn,
        returnAgainstId: isReturn ? returnAgainstId : null,
        updateStock: !isReturn && updateStock,
        sourceWarehouseId: !isReturn && updateStock ? sourceWarehouseId : null,
        advances:
          isReturn || advanceRows.length === 0
            ? []
            : advanceRows.map((row) => ({
                paymentEntryId: row.paymentEntryId,
                allocatedAmount: Number(row.amount) || 0,
              })),
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
                <label className="text-sm font-medium">{t('form.customer', 'Customer *')}</label>
                <select
                  value={customerId}
                  onChange={(e) => {
                    setCustomerId(e.target.value)
                    setReturnAgainstId('')
                  }}
                  className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  required
                >
                  <option value="">{t('form.selectNone', '-- Select --')}</option>
                  {customersQuery.items.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.code} — {c.name}
                    </option>
                  ))}
                </select>
              </div>
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('form.postingDate', 'Posting date *')}</label>
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
                <label className="text-sm font-medium">{t('form.lines', 'Lines')}</label>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setLines((prev) => [...prev, { itemId: '', quantity: isReturn ? -1 : 1, rate: 0 }])
                  }
                >
                  {t('form.addLine', 'Add line')}
                </Button>
              </div>
              {lines.map((line, i) => (
                <div key={i} className="grid grid-cols-12 gap-2">
                  <select
                    value={line.itemId}
                    onChange={(e) => updateLine(i, { itemId: e.target.value })}
                    className="col-span-6 flex h-10 rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  >
                    <option value="">{t('form.selectItem', '-- Item --')}</option>
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
                    placeholder={t('form.qty', 'Qty')}
                    className="col-span-3"
                  />
                  <Input
                    type="number"
                    min={0}
                    step="0.0001"
                    value={line.rate}
                    onChange={(e) => updateLine(i, { rate: Number(e.target.value) })}
                    placeholder={t('form.rate', 'Rate')}
                    className="col-span-3"
                  />
                </div>
              ))}
            </div>

            {/* Immediate stock update: delivery from a source warehouse */}
            {!isReturn && (
              <div className="space-y-3 rounded-lg border border-slate-200 bg-slate-50 p-3">
                <label className="flex cursor-pointer items-center gap-2 text-sm font-medium text-slate-800">
                  <input
                    type="checkbox"
                    className="h-4 w-4 accent-sky-600"
                    checked={updateStock}
                    onChange={(e) => {
                      setUpdateStock(e.target.checked)
                      if (!e.target.checked) setSourceWarehouseId('')
                    }}
                  />
                  {t('invoiceForm.updateStock', 'Update stock (immediate delivery)')}
                </label>
                {updateStock && (
                  <div className="space-y-2">
                    <label className="text-sm font-medium">
                      {t('invoiceForm.sourceWarehouse', 'Source warehouse *')}
                    </label>
                    <select
                      value={sourceWarehouseId}
                      onChange={(e) => setSourceWarehouseId(e.target.value)}
                      className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                    >
                      <option value="">{t('form.selectNone', '-- Select --')}</option>
                      {warehousesQuery.items.map((w) => (
                        <option key={w.id} value={w.id}>
                          {w.code} — {w.name}
                        </option>
                      ))}
                    </select>
                  </div>
                )}
              </div>
            )}

            {/* Advance allocation: cross unallocated customer receipts */}
            {!isReturn && (
              <div className="space-y-3 rounded-lg border border-slate-200 bg-slate-50 p-3">
                <div className="flex items-center justify-between">
                  <label className="text-sm font-medium text-slate-800">
                    {t('invoiceForm.advancesTitle', 'Advance cross')}
                  </label>
                  {advanceRows.length > 0 && (
                    <span className="font-mono text-xs text-slate-600">
                      {allocatedTotal.toLocaleString()} / {Math.abs(preview.grand).toLocaleString()}
                    </span>
                  )}
                </div>
                {!customerId && (
                  <p className="text-xs text-slate-500">
                    {t('invoiceForm.advancesNeedCustomer', 'Select a customer to list their advances.')}
                  </p>
                )}
                {customerId && eligibleQuery.status === 'loading' && (
                  <p className="text-xs text-slate-500">
                    {t('invoiceForm.advancesLoading', 'Loading advances…')}
                  </p>
                )}
                {customerId && eligibleQuery.status !== 'loading' && eligibleAdvances.length === 0 && (
                  <p className="text-xs text-slate-500">
                    {t('invoiceForm.advancesEmpty', 'No unallocated advances for this customer.')}
                  </p>
                )}
                {eligibleAdvances.map((advance) => {
                  const selected = advanceRows.some(
                    (row) => row.paymentEntryId === advance.paymentEntryId,
                  )
                  const row = advanceRows.find(
                    (r) => r.paymentEntryId === advance.paymentEntryId,
                  )
                  return (
                    <div key={advance.paymentEntryId} className="grid grid-cols-12 items-center gap-2">
                      <label className="col-span-7 flex cursor-pointer items-center gap-2 text-sm text-slate-800">
                        <input
                          type="checkbox"
                          className="h-4 w-4 accent-sky-600"
                          checked={selected}
                          onChange={() => toggleAdvance(advance)}
                        />
                        <span className="font-mono">{advance.voucherNo}</span>
                        <span className="text-xs text-slate-500">
                          {advance.paymentDate} · {advance.unallocatedAmount.toLocaleString()}
                        </span>
                      </label>
                      <Input
                        type="number"
                        min={0}
                        max={advance.unallocatedAmount}
                        step="0.01"
                        value={row ? row.amount : advance.unallocatedAmount}
                        disabled={!selected}
                        onChange={(e) => updateAdvanceAmount(advance.paymentEntryId, Number(e.target.value))}
                        placeholder={t('invoiceForm.advancesAllocated', 'Allocated')}
                        className="col-span-5"
                        aria-label={t('invoiceForm.advancesAllocated', 'Allocated')}
                      />
                    </div>
                  )
                })}
              </div>
            )}

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

            {/* Tax-engine Fase 2: template shortcut + Taxes and Charges grid */}
            <div className="space-y-2">
              <TaxTemplateSelector
                companyId={companyId}
                category="Sales"
                value={templateId}
                onSelect={applyTemplate}
              />
              <div className="flex items-center justify-between">
                <label className="text-sm font-medium">
                  {t('invoiceForm.taxesTitle', 'Taxes and Charges')}
                </label>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setTaxes((prev) => [
                      ...prev,
                      { accountId: '', rate: 21, chargeType: 'Add', basis: 'OnNetTotal', fixedAmount: 0 },
                    ])
                  }
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
                    className="col-span-4 flex h-10 rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                    aria-label={t('invoiceForm.taxAccountPlaceholder', '-- Posting account --')}
                  >
                    <option value="">
                      {t('invoiceForm.taxAccountPlaceholder', '-- Posting account --')}
                    </option>
                    {taxAccounts.map((a) => (
                      <option key={a.id} value={a.id}>
                        {a.label}
                      </option>
                    ))}
                  </select>
                  <select
                    value={row.chargeType}
                    onChange={(e) => updateTax(i, { chargeType: e.target.value as TaxDraft['chargeType'] })}
                    className="col-span-2 flex h-10 rounded-md border border-slate-200 bg-white px-2 py-2 text-sm"
                    aria-label={t('invoiceForm.taxChargeLabel', 'Charge type')}
                  >
                    <option value="Add">{t('invoiceForm.chargeAdd', 'Traslado')}</option>
                    <option value="Deduct">{t('invoiceForm.chargeDeduct', 'Retención')}</option>
                  </select>
                  <select
                    value={row.basis}
                    onChange={(e) => updateTax(i, { basis: e.target.value as TaxDraft['basis'] })}
                    className="col-span-2 flex h-10 rounded-md border border-slate-200 bg-white px-2 py-2 text-sm"
                    aria-label={t('invoiceForm.taxBasisLabel', 'Calculation basis')}
                  >
                    <option value="OnNetTotal">{t('invoiceForm.basisNet', 'On net')}</option>
                    <option value="OnPreviousRow">{t('invoiceForm.basisPrevious', 'Cascade')}</option>
                    <option value="FixedAmount">{t('invoiceForm.basisFixed', 'Fixed')}</option>
                  </select>
                  <Input
                    type="number"
                    min={0}
                    max={row.basis === 'FixedAmount' ? undefined : 100}
                    step="0.01"
                    value={row.basis === 'FixedAmount' ? row.fixedAmount : row.rate}
                    onChange={(e) =>
                      row.basis === 'FixedAmount'
                        ? updateTax(i, { fixedAmount: Number(e.target.value) })
                        : updateTax(i, { rate: Number(e.target.value) })
                    }
                    placeholder={row.basis === 'FixedAmount' ? t('invoiceForm.fixedPlaceholder', 'Amount') : '%'}
                    className="col-span-3"
                    aria-label={
                      row.basis === 'FixedAmount'
                        ? t('invoiceForm.fixedPlaceholder', 'Amount')
                        : t('invoiceForm.ratePlaceholder', 'Rate %')
                    }
                  />
                  <div className="col-span-1 flex items-center justify-end gap-2">
                    <span className="font-mono text-xs text-slate-600">
                      {preview.calc.rows[i]?.amount.toLocaleString() ?? '0'}
                    </span>
                    <button
                      type="button"
                      className="text-xs text-red-600 hover:underline"
                      onClick={() => setTaxes((prev) => prev.filter((_, j) => j !== i))}
                      aria-label={t('invoiceForm.removeTax', 'Remove tax row')}
                    >
                      ✕
                    </button>
                  </div>
                </div>
              ))}
              {taxes.length > 0 && (
                <TaxBreakdownTable
                  netBase={preview.base}
                  rows={taxes.map((row) => ({
                    rate: Number(row.rate) || 0,
                    chargeType: row.chargeType,
                    basis: row.basis,
                    fixedAmount: Number(row.fixedAmount) || 0,
                    accountLabel: taxAccountLabel(row.accountId),
                  }))}
                />
              )}
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
                <span>{t('invoiceForm.totalAdd', 'Transferred taxes')}</span>
                <span>+{preview.calc.addTotal.toLocaleString()}</span>
              </div>
              <div className="flex justify-between text-amber-700">
                <span>{t('invoiceForm.totalDeduct', 'Retentions')}</span>
                <span>−{preview.calc.deductTotal.toLocaleString()}</span>
              </div>
              <div className="flex justify-between border-t border-slate-100 pt-1 text-sm font-bold text-slate-900">
                <span>{t('invoiceForm.totalGrand', 'Grand Total')}</span>
                <span>{preview.grand.toLocaleString()}</span>
              </div>
            </div>
          </div>

          <DialogFooter className="border-t border-slate-100 pt-4">
            <Button type="button" variant="outline" onClick={onClose}>
              {t('form.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('form.saving', 'Saving…') : t('form.save', 'Save')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
