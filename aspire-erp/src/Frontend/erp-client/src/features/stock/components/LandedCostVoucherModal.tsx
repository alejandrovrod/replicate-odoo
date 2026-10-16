import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { ApiError } from '../../../api/client'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { useAccountTree } from '../../accounting/useAccountTree'
import type { AccountTreeNode } from '../../accounting/types'
import {
  usePurchaseReceiptOptions,
  type CreateLandedCostPayload,
  type LandedCostDistributeBy,
} from '../api/useLandedCostVouchers'
import { apportionLandedCost } from '../landedCostCalculation'

interface LandedCostVoucherModalProps {
  isOpen: boolean
  onClose: () => void
  onSave: (data: CreateLandedCostPayload) => Promise<void>
  companyId: string
}

interface ChargeDraft {
  key: string
  description: string
  amount: number
  expenseAccountId: string
}

let chargeSequence = 0
const nextChargeKey = (): string => `charge-${++chargeSequence}`

function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

/** Posting leaves of any root type (bridge/expense/clearing accounts vary by chart). */
function postingLeaves(nodes: AccountTreeNode[]): { id: string; label: string }[] {
  const out: { id: string; label: string }[] = []
  const walk = (list: AccountTreeNode[]) => {
    for (const node of list) {
      if (!node.isGroup && node.isActive) out.push({ id: node.id, label: `${node.code} — ${node.name}` })
      if (node.children.length > 0) walk(node.children)
    }
  }
  walk(nodes)
  return out.sort((a, b) => a.label.localeCompare(b.label))
}

const selectClassName =
  'flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50'

/**
 * Landed cost voucher composer (Fase 4, gap D5): multi-receipt picker (Submitted only),
 * freight/insurance/customs charge grid with bridge accounts, Amount/Quantity strategy
 * radio and a live before/after comparison table. The server apportions authoritatively
 * at Draft creation - this preview mirrors its exact-cents rule.
 */
export function LandedCostVoucherModal({ isOpen, onClose, onSave, companyId }: LandedCostVoucherModalProps) {
  const { t, i18n } = useTranslation('stock')
  const { t: tBuying } = useTranslation('buying')
  const [postingDate, setPostingDate] = useState(todayIso())
  const [distributeBy, setDistributeBy] = useState<LandedCostDistributeBy>('Amount')
  const [receiptIds, setReceiptIds] = useState<string[]>([])
  const [charges, setCharges] = useState<ChargeDraft[]>([
    { key: nextChargeKey(), description: '', amount: 0, expenseAccountId: '' },
  ])
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  const receiptsQuery = usePurchaseReceiptOptions(companyId)
  const accountsQuery = useAccountTree(companyId)
  const expenseAccounts = useMemo(
    () => postingLeaves(accountsQuery.nodes ?? []),
    [accountsQuery.nodes],
  )

  const submittedReceipts = useMemo(
    () => (receiptsQuery.status === 'success' ? receiptsQuery.items : []).filter((r) => r.status === 'Submitted'),
    [receiptsQuery],
  )

  useEffect(() => {
    if (isOpen) {
      setPostingDate(todayIso())
      setDistributeBy('Amount')
      setReceiptIds([])
      setCharges([{ key: nextChargeKey(), description: '', amount: 0, expenseAccountId: '' }])
      setError(null)
    }
  }, [isOpen])

  const selectedLines = useMemo(
    () =>
      submittedReceipts
        .filter((r) => receiptIds.includes(r.id))
        .flatMap((r) =>
          r.lines.map((l) => ({
            key: `${r.id}:${l.id}`,
            receiptNo: r.voucherNo,
            itemCode: l.itemCode,
            itemName: l.itemName,
            qty: l.qty,
            amount: l.qty * l.rate,
            originalRate: l.rate,
          })),
        ),
    [submittedReceipts, receiptIds],
  )

  const totalCharges = useMemo(
    () => charges.reduce((sum, c) => sum + (Number(c.amount) || 0), 0),
    [charges],
  )

  const preview = useMemo(
    () => apportionLandedCost(selectedLines, totalCharges, distributeBy),
    [selectedLines, totalCharges, distributeBy],
  )

  const toggleReceipt = (id: string) =>
    setReceiptIds((prev) => (prev.includes(id) ? prev.filter((r) => r !== id) : [...prev, id]))

  const updateCharge = (key: string, patch: Partial<ChargeDraft>) =>
    setCharges((prev) => prev.map((c) => (c.key === key ? { ...c, ...patch } : c)))

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault()
    setError(null)
    if (receiptIds.length === 0) {
      setError(t('landedCost.errorReceipts', 'Select at least one submitted purchase receipt.'))
      return
    }
    const validCharges = charges.filter((c) => c.description.trim() && Number(c.amount) > 0)
    if (validCharges.length === 0) {
      setError(t('landedCost.errorCharges', 'Add at least one charge with an amount greater than zero.'))
      return
    }
    if (validCharges.some((c) => !c.expenseAccountId)) {
      setError(t('landedCost.errorAccount', 'Every charge needs a bridge/expense account.'))
      return
    }
    setIsSubmitting(true)
    try {
      await onSave({
        companyId,
        postingDate,
        distributeBy,
        receiptIds,
        charges: validCharges.map((c) => ({
          description: c.description.trim(),
          amount: Number(c.amount),
          expenseAccountId: c.expenseAccountId,
        })),
      })
      onClose()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setError(translateErrorCode(i18n, err.code, err.message))
      } else if (err instanceof Error) {
        setError(err.message)
      } else {
        setError(t('landedCost.errorGeneric', 'An error occurred while saving.'))
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-3xl">
        <DialogHeader>
          <DialogTitle>{t('landedCost.titleNew', 'New Landed Cost Voucher')}</DialogTitle>
        </DialogHeader>

        <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden min-h-0">
          <div className="flex-1 overflow-y-auto pr-2 space-y-4">
            {error && <div className="text-sm text-red-600">{error}</div>}

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <label className="text-sm font-medium">{t('landedCost.dateLabel', 'Posting date *')}</label>
                <Input type="date" value={postingDate} onChange={(e) => setPostingDate(e.target.value)} required />
              </div>
              <fieldset className="space-y-2">
                <legend className="text-sm font-medium">{t('landedCost.strategyLabel', 'Apportion by')}</legend>
                <div className="flex gap-4 pt-2">
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="radio"
                      name="distributeBy"
                      value="Amount"
                      checked={distributeBy === 'Amount'}
                      onChange={() => setDistributeBy('Amount')}
                      className="h-4 w-4"
                    />
                    {t('landedCost.strategyAmount', 'Amount (value)')}
                  </label>
                  <label className="flex items-center gap-2 text-sm">
                    <input
                      type="radio"
                      name="distributeBy"
                      value="Quantity"
                      checked={distributeBy === 'Quantity'}
                      onChange={() => setDistributeBy('Quantity')}
                      className="h-4 w-4"
                    />
                    {t('landedCost.strategyQuantity', 'Quantity')}
                  </label>
                </div>
              </fieldset>
            </div>

            <div className="space-y-2">
              <label className="text-sm font-medium">
                {tBuying('landedCost.receiptsLabel', 'Purchase receipts (submitted)')}
              </label>
              {receiptsQuery.status === 'loading' ? (
                <p className="text-xs text-slate-500">{tBuying('landedCost.receiptsLoading', 'Loading receipts…')}</p>
              ) : submittedReceipts.length === 0 ? (
                <p className="text-xs text-slate-500">
                  {tBuying('landedCost.receiptsEmpty', 'No submitted purchase receipts for this company yet.')}
                </p>
              ) : (
                <div className="max-h-32 space-y-1 overflow-y-auto rounded-lg border border-slate-200 p-2">
                  {submittedReceipts.map((receipt) => (
                    <label key={receipt.id} className="flex cursor-pointer items-center gap-2 text-sm">
                      <input
                        type="checkbox"
                        checked={receiptIds.includes(receipt.id)}
                        onChange={() => toggleReceipt(receipt.id)}
                        className="h-4 w-4 rounded border-slate-300"
                      />
                      <span className="font-mono font-medium">{receipt.voucherNo}</span>
                      <span className="font-mono text-xs text-slate-500">
                        {receipt.postingDate} ·{' '}
                        {tBuying('landedCost.receiptLines', '{{count}} lines', { count: receipt.lines.length })}
                      </span>
                    </label>
                  ))}
                </div>
              )}
            </div>

            <div className="space-y-2">
              <div className="flex items-center justify-between">
                <label className="text-sm font-medium">{t('landedCost.chargesLabel', 'Charges')}</label>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() =>
                    setCharges((prev) => [...prev, { key: nextChargeKey(), description: '', amount: 0, expenseAccountId: '' }])
                  }
                >
                  {t('landedCost.addCharge', 'Add charge')}
                </Button>
              </div>
              {charges.map((charge) => (
                <div key={charge.key} className="grid grid-cols-12 items-center gap-2">
                  <Input
                    value={charge.description}
                    onChange={(e) => updateCharge(charge.key, { description: e.target.value })}
                    placeholder={t('landedCost.chargePlaceholder', 'Ocean freight, insurance, customs…')}
                    className="col-span-5"
                    aria-label={t('landedCost.chargePlaceholder', 'Ocean freight, insurance, customs…')}
                  />
                  <Input
                    type="number"
                    min={0}
                    step="0.01"
                    value={charge.amount || ''}
                    onChange={(e) => updateCharge(charge.key, { amount: Number(e.target.value) })}
                    placeholder="0.00"
                    className="col-span-2"
                    aria-label={t('landedCost.amountLabel', 'Amount')}
                  />
                  <select
                    value={charge.expenseAccountId}
                    onChange={(e) => updateCharge(charge.key, { expenseAccountId: e.target.value })}
                    disabled={accountsQuery.status !== 'success'}
                    className={`${selectClassName} col-span-4`}
                    aria-label={t('landedCost.accountLabel', 'Bridge/expense account')}
                  >
                    <option value="">{t('landedCost.accountNone', '-- Select account --')}</option>
                    {expenseAccounts.map((account) => (
                      <option key={account.id} value={account.id}>
                        {account.label}
                      </option>
                    ))}
                  </select>
                  <button
                    type="button"
                    className="col-span-1 text-xs text-red-600 hover:underline disabled:opacity-40"
                    disabled={charges.length === 1}
                    onClick={() => setCharges((prev) => prev.filter((c) => c.key !== charge.key))}
                    aria-label={t('landedCost.removeCharge', 'Remove charge')}
                  >
                    ✕
                  </button>
                </div>
              ))}
            </div>

            {selectedLines.length > 0 && totalCharges > 0 && (
              <div className="space-y-2">
                <label className="text-sm font-medium">
                  {t('landedCost.previewTitle', 'Apportionment preview (before → after)')}
                </label>
                <div className="overflow-x-auto rounded-lg border border-slate-200">
                  <table className="w-full min-w-[560px] text-left text-xs">
                    <thead>
                      <tr className="border-b border-slate-200 bg-slate-50 uppercase tracking-wide text-slate-500">
                        <th className="px-3 py-2 font-semibold">{t('landedCost.colItem', 'Item')}</th>
                        <th className="px-3 py-2 text-right font-semibold">{t('landedCost.colQty', 'Qty')}</th>
                        <th className="px-3 py-2 text-right font-semibold">{t('landedCost.colOldRate', 'Old rate')}</th>
                        <th className="px-3 py-2 text-right font-semibold">{t('landedCost.colCharges', 'Charges')}</th>
                        <th className="px-3 py-2 text-right font-semibold">{t('landedCost.colNewRate', 'New rate')}</th>
                      </tr>
                    </thead>
                    <tbody>
                      {preview.map((row) => (
                        <tr key={row.key} className="border-b border-slate-100 last:border-b-0">
                          <td className="px-3 py-2 text-slate-900">
                            <span className="font-mono text-slate-600">{row.itemCode}</span>
                            <span className="ml-2">{row.itemName}</span>
                            <span className="ml-2 font-mono text-[10px] text-slate-400">{row.receiptNo}</span>
                          </td>
                          <td className="px-3 py-2 text-right font-mono">{row.qty.toLocaleString()}</td>
                          <td className="px-3 py-2 text-right font-mono text-slate-600">
                            {row.originalRate.toLocaleString()}
                          </td>
                          <td className="px-3 py-2 text-right font-mono font-semibold text-sky-700">
                            +{row.applicableCharges.toLocaleString()}
                          </td>
                          <td className="px-3 py-2 text-right font-mono font-bold text-slate-900">
                            {row.newRate.toLocaleString()}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                <p className="text-right font-mono text-xs text-slate-600">
                  {t('landedCost.previewTotal', 'Total charges: {{total}}', {
                    total: totalCharges.toLocaleString(),
                  })}
                </p>
              </div>
            )}
          </div>

          <DialogFooter className="pt-4 border-t border-slate-100">
            <Button type="button" variant="outline" onClick={onClose}>
              {t('landedCost.cancel', 'Cancel')}
            </Button>
            <Button type="submit" disabled={isSubmitting}>
              {isSubmitting ? t('landedCost.saving', 'Saving…') : t('landedCost.save', 'Save Draft')}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  )
}
