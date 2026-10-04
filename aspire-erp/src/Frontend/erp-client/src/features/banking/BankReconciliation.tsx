import { useCallback, useEffect, useState } from 'react'
import { ArrowDownLeft, ArrowUpRight, PlusCircle, RefreshCw } from 'lucide-react'
import { ApiError, apiClient } from '../../api/client'
import { useErpAction } from '../../lib/useErpAction'
import { useTenantStore } from '../../store/useTenantStore'
import type { BankTransaction, BankTransactionStatus, ReconciliationSummary } from './types'
import { netAmount } from './types'
import { VoucherQuickCreateDialog } from './VoucherQuickCreateDialog'

interface AllocationSlice {
  paymentEntryId: string
  glVoucherId: string
  amount: string
}

const EMPTY_SLICE: AllocationSlice = { paymentEntryId: '', glVoucherId: '', amount: '' }

const STATUS_FILTERS: BankTransactionStatus[] = ['Unreconciled', 'Matched', 'Reconciled', 'Excluded']

/**
 * Dual-sided reconciliation grid (task 6.6): the left side lists staging lines, the right
 * side edits allocation slices against payment vouchers or posted GL vouchers with a live
 * difference-to-zero indicator (BN-04).
 *
 * Scope note: there is no payment-entry LIST endpoint, so counterpart ids are typed/pasted
 * (a GUID input per slice) rather than picked from a dropdown; either a PaymentEntryId or a
 * GL voucher id may fund each slice, never both.
 */
export function BankReconciliation({ refreshSignal }: { refreshSignal: number }) {
  const companyId = useTenantStore((state) => state.companyId)
  const [statusFilter, setStatusFilter] = useState<BankTransactionStatus>('Unreconciled')
  const [lines, setLines] = useState<BankTransaction[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [slices, setSlices] = useState<AllocationSlice[]>([{ ...EMPTY_SLICE }])
  const [showVoucherDialog, setShowVoucherDialog] = useState(false)

  const load = useCallback(async () => {
    if (!companyId) return
    setIsLoading(true)
    setLoadError(null)
    try {
      const response = await apiClient.get<BankTransaction[]>('/v1/bank-transactions', {
        params: { companyId, status: statusFilter },
      })
      setLines(response.data)
    } catch (err) {
      setLoadError(err instanceof ApiError ? err.message : 'Failed to load staging lines.')
    } finally {
      setIsLoading(false)
    }
  }, [companyId, statusFilter])

  useEffect(() => {
    void load()
  }, [load, refreshSignal])

  const selected = lines.find((l) => l.id === selectedId) ?? null
  const expected = selected ? netAmount(selected) : 0
  const allocated = slices.reduce((sum, s) => sum + (Number(s.amount) || 0), 0)
  const difference = expected - allocated

  const setSlice = (index: number, patch: Partial<AllocationSlice>) =>
    setSlices((prev) => prev.map((s, i) => (i === index ? { ...s, ...patch } : s)))

  const toPayloadLines = () =>
    slices
      .filter((s) => Number(s.amount) > 0)
      .map((s) => ({
        paymentEntryId: s.paymentEntryId.trim() === '' ? null : s.paymentEntryId.trim(),
        glVoucherId: s.glVoucherId.trim() === '' ? null : s.glVoucherId.trim(),
        amount: Number(s.amount),
      }))

  const slicesValid =
    selected !== null &&
    slices.some((s) => Number(s.amount) > 0) &&
    slices.every((s) => {
      if (Number(s.amount) <= 0) return true
      const hasEntry = s.paymentEntryId.trim() !== ''
      const hasGl = s.glVoucherId.trim() !== ''
      return hasEntry !== hasGl
    }) &&
    difference === 0

  const { state: reconcileState, dispatch: confirmReconcile, isPending: isReconciling } =
    useErpAction<ReconciliationSummary, void>(async () => {
      if (!selected) throw new Error('No staging line selected.')
      const response = await apiClient.post<ReconciliationSummary>(
        `/v1/bank-transactions/${selected.id}/reconcile`,
        { companyId, lines: toPayloadLines(), rowVersion: selected.rowVersion },
        { headers: { 'Idempotency-Key': crypto.randomUUID() } },
      )
      setSlices([{ ...EMPTY_SLICE }])
      await load()
      return response.data
    })

  const { state: unreconcileState, dispatch: confirmUnreconcile, isPending: isUnreconciling } =
    useErpAction<unknown, void>(async () => {
      if (!selected) throw new Error('No staging line selected.')
      const response = await apiClient.post(
        `/v1/bank-transactions/${selected.id}/unreconcile`,
        { companyId, rowVersion: selected.rowVersion },
        { headers: { 'Idempotency-Key': crypto.randomUUID() } },
      )
      setSlices([{ ...EMPTY_SLICE }])
      await load()
      return response.data
    })

  return (
    <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
      {/* Left: staging lines */}
      <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
        <div className="flex items-center justify-between gap-2">
          <h3 className="text-sm font-bold text-slate-900">Staging lines</h3>
          <div className="flex items-center gap-2">
            <select
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as BankTransactionStatus)}
              className="rounded-lg border border-slate-300 bg-white px-2 py-1 text-xs"
              aria-label="Status filter"
            >
              {STATUS_FILTERS.map((s) => (
                <option key={s} value={s}>{s}</option>
              ))}
            </select>
            <button
              type="button"
              onClick={() => load()}
              className="rounded-lg border border-slate-300 p-1.5 text-slate-500 hover:bg-slate-50"
              aria-label="Reload staging lines"
            >
              <RefreshCw className="h-3.5 w-3.5" />
            </button>
          </div>
        </div>

        {isLoading && <p className="mt-4 text-xs text-slate-500">Loading staging lines…</p>}
        {loadError && (
          <div className="mt-4 rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700">
            {loadError}
          </div>
        )}
        {!isLoading && !loadError && lines.length === 0 && (
          <p className="mt-4 text-xs text-slate-500">No {statusFilter.toLowerCase()} lines.</p>
        )}

        <ul className="mt-3 max-h-96 space-y-2 overflow-y-auto">
          {lines.map((line) => (
            <li key={line.id}>
              <button
                type="button"
                onClick={() => {
                  setSelectedId(line.id)
                  setSlices([{ ...EMPTY_SLICE }])
                }}
                className={`w-full rounded-lg border p-3 text-left hover:border-sky-300 ${
                  line.id === selectedId ? 'border-sky-500 bg-sky-50' : 'border-slate-200'
                }`}
              >
                <div className="flex items-center justify-between gap-2">
                  <span className="font-mono text-[11px] text-slate-500">{line.transactionDate}</span>
                  <span
                    className={`inline-flex items-center gap-1 rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                      line.deposit > 0 ? 'bg-emerald-50 text-emerald-700' : 'bg-rose-50 text-rose-700'
                    }`}
                  >
                    {line.deposit > 0 ? <ArrowDownLeft className="h-3 w-3" /> : <ArrowUpRight className="h-3 w-3" />}
                    {line.deposit > 0 ? 'Deposit' : 'Withdrawal'}
                  </span>
                </div>
                <p className="mt-1 text-xs font-semibold text-slate-900">{line.description}</p>
                <div className="mt-1 flex items-center justify-between">
                  <span className="font-mono text-xs font-bold text-slate-900">
                    {netAmount(line).toFixed(2)} {line.currency}
                  </span>
                  <span className="rounded bg-slate-100 px-2 py-0.5 text-[11px] font-medium text-slate-700">
                    {line.status}
                    {line.suggestedPartyType ? ` · ${line.suggestedPartyType}` : ''}
                  </span>
                </div>
              </button>
            </li>
          ))}
        </ul>
      </div>

      {/* Right: counterpart picker */}
      <div className="rounded-xl border border-slate-200 bg-white p-5 shadow-xs">
        <h3 className="text-sm font-bold text-slate-900">Counterparts</h3>
        {!selected ? (
          <p className="mt-4 text-xs text-slate-500">Select a staging line to allocate it.</p>
        ) : (
          <div className="mt-3 space-y-3">
            <p className="font-mono text-xs text-slate-600">
              {selected.description} · expected {expected.toFixed(2)} {selected.currency}
            </p>
            {slices.map((slice, index) => (
              <div key={index} className="grid grid-cols-1 gap-2 rounded-lg border border-slate-200 p-3">
                <label className="block text-[11px] font-semibold text-slate-600">
                  Payment entry id (or GL voucher id below — exactly one)
                  <input
                    value={slice.paymentEntryId}
                    onChange={(e) => setSlice(index, { paymentEntryId: e.target.value })}
                    placeholder="PaymentEntry GUID"
                    className="mt-1 w-full rounded-lg border border-slate-300 px-2 py-1.5 font-mono text-xs font-normal"
                  />
                </label>
                <label className="block text-[11px] font-semibold text-slate-600">
                  GL voucher id
                  <input
                    value={slice.glVoucherId}
                    onChange={(e) => setSlice(index, { glVoucherId: e.target.value })}
                    placeholder="JournalEntry GUID"
                    className="mt-1 w-full rounded-lg border border-slate-300 px-2 py-1.5 font-mono text-xs font-normal"
                  />
                </label>
                <label className="block text-[11px] font-semibold text-slate-600">
                  Amount
                  <input
                    value={slice.amount}
                    onChange={(e) => setSlice(index, { amount: e.target.value })}
                    inputMode="decimal"
                    className="mt-1 w-full rounded-lg border border-slate-300 px-2 py-1.5 font-mono text-xs font-normal"
                  />
                </label>
              </div>
            ))}
            <button
              type="button"
              onClick={() => setSlices((prev) => [...prev, { ...EMPTY_SLICE }])}
              className="flex items-center gap-1 text-xs font-semibold text-sky-600 hover:text-sky-700"
            >
              <PlusCircle className="h-3.5 w-3.5" /> Add slice
            </button>

            <div
              className={`rounded-lg p-3 font-mono text-xs font-bold ${
                difference === 0 ? 'bg-emerald-50 text-emerald-700' : 'bg-amber-50 text-amber-700'
              }`}
            >
              Difference: {difference.toFixed(2)} {difference === 0 ? '· ready to confirm' : '· must be $0.00'}
            </div>

            {(reconcileState.error ?? unreconcileState.error) && (
              <div className="rounded-lg border border-rose-200 bg-rose-50 p-3 text-xs text-rose-700">
                {reconcileState.error ?? unreconcileState.error}{' '}
                ({reconcileState.errorCode ?? unreconcileState.errorCode})
              </div>
            )}

            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                disabled={!slicesValid || isReconciling}
                onClick={() => confirmReconcile()}
                className="rounded-lg bg-sky-600 px-3 py-2 text-xs font-semibold text-white hover:bg-sky-700 disabled:opacity-50"
              >
                {isReconciling ? 'Confirming…' : 'Confirm reconcile'}
              </button>
              <button
                type="button"
                disabled={selected.status !== 'Reconciled' || isUnreconciling}
                onClick={() => confirmUnreconcile()}
                className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50"
              >
                {isUnreconciling ? 'Reverting…' : 'Un-reconcile'}
              </button>
              <button
                type="button"
                disabled={selected.status === 'Reconciled'}
                onClick={() => setShowVoucherDialog(true)}
                className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50"
              >
                Quick voucher
              </button>
            </div>
            <p className="text-[11px] text-slate-400">
              No payment-entry browser exists yet: paste a PaymentEntry id (or a posted GL
              voucher id) per slice.
            </p>
          </div>
        )}
      </div>

      {showVoucherDialog && selected && (
        <VoucherQuickCreateDialog
          transaction={selected}
          onClose={() => setShowVoucherDialog(false)}
          onCreated={() => load()}
        />
      )}
    </div>
  )
}
