import { ArrowRight, CheckCircle2, ClipboardList, PackageCheck, Truck } from 'lucide-react'
import { useState } from 'react'
import { useErpAction } from '../../lib/useErpAction'
import type { ManufacturingPosting, WorkOrder, WorkOrderStatus } from './types'
import {
  postCancelWorkOrder,
  postCompleteManufacture,
  postSubmitWorkOrder,
  postTransferToWip,
  useWorkOrders,
} from './useManufacturingData'

const STATUS_BADGE: Record<WorkOrderStatus, string> = {
  Draft: 'bg-slate-100 text-slate-700',
  Submitted: 'bg-sky-100 text-sky-700',
  InProcess: 'bg-amber-100 text-amber-800',
  Completed: 'bg-emerald-100 text-emerald-700',
  Cancelled: 'bg-rose-100 text-rose-700',
}

/**
 * Work order execution board (Task 9.5): live headers from GET /api/v1/workorders with
 * the status-flow actions (submit / transfer / complete / cancel) one click away. Every
 * mutation posts with a FRESH `Idempotency-Key` (Constitution VI.4) and reloads the list,
 * which is what makes the new status appear in real time.
 */
export function WorkOrdersBoard({ companyId }: { companyId: string }) {
  const ordersQuery = useWorkOrders(companyId)
  const [selectedId, setSelectedId] = useState<string | null>(null)
  const [producedQty, setProducedQty] = useState('')

  const selected: WorkOrder | null =
    ordersQuery.data.find((o) => o.id === selectedId) ?? null

  const refresh = () => {
    setSelectedId(null)
    ordersQuery.reload()
  }

  const { state: submitState, dispatch: submit, isPending: isSubmitting } = useErpAction<
    WorkOrder,
    string
  >(async (orderId) => {
    const order = await postSubmitWorkOrder(orderId, companyId)
    refresh()
    return order
  })

  const { state: transferState, dispatch: transfer, isPending: isTransferring } = useErpAction<
    ManufacturingPosting,
    string
  >(async (orderId) => {
    const posting = await postTransferToWip(orderId, companyId)
    refresh()
    return posting
  })

  const { state: completeState, dispatch: complete, isPending: isCompleting } = useErpAction<
    ManufacturingPosting,
    { orderId: string; qty: number }
  >(async ({ orderId, qty }) => {
    const posting = await postCompleteManufacture(orderId, companyId, qty)
    refresh()
    return posting
  })

  const { state: cancelState, dispatch: cancel, isPending: isCancelling } = useErpAction<
    WorkOrder,
    string
  >(async (orderId) => {
    const order = await postCancelWorkOrder(orderId, companyId)
    refresh()
    return order
  })

  const lastError =
    submitState.error ?? transferState.error ?? completeState.error ?? cancelState.error
  const lastErrorCode =
    submitState.errorCode ?? transferState.errorCode ?? completeState.errorCode ?? cancelState.errorCode
  const lastPosting =
    transferState.data ?? completeState.data ?? null

  if (ordersQuery.status === 'error') {
    return (
      <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
        <p className="font-medium">
          Could not load work orders{ordersQuery.error?.status ? ` (HTTP ${ordersQuery.error.status})` : ''}.
        </p>
        {ordersQuery.error?.message ? <p className="mt-1">{ordersQuery.error.message}</p> : null}
        <button
          type="button"
          onClick={() => ordersQuery.reload()}
          className="mt-2 rounded border border-rose-400 px-2 py-1 text-rose-900 hover:bg-rose-100"
        >
          Retry
        </button>
      </div>
    )
  }

  if (ordersQuery.status !== 'success') {
    return <p className="px-1 py-3 text-sm text-slate-500">Loading work orders…</p>
  }

  return (
    <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
      <div className="mb-4 flex flex-wrap items-end justify-between gap-3">
        <div>
          <h3 className="flex items-center gap-2 text-base font-semibold text-slate-900">
            <ClipboardList className="h-4 w-4 text-indigo-600" />
            Work Orders
          </h3>
          <p className="text-xs text-slate-500">
            Shop floor execution: Draft → Submitted → InProcess → Completed (or Cancelled).
          </p>
        </div>
      </div>

      {lastPosting ? (
        <p className="mb-3 flex items-start gap-2 rounded-md border border-emerald-300 bg-emerald-50 px-4 py-2.5 text-xs text-emerald-900">
          <CheckCircle2 className="mt-0.5 size-4 shrink-0" aria-hidden="true" />
          <span>
            Posted <span className="font-mono font-semibold">{lastPosting.entry.voucherNo}</span> —
            Dr {lastPosting.totalDebit} = Cr {lastPosting.totalCredit}. List refreshed.
          </span>
        </p>
      ) : null}

      {lastError ? (
        <p className="mb-3 rounded-md border border-rose-300 bg-rose-50 px-4 py-2.5 text-xs text-rose-800">
          {lastError}{lastErrorCode ? ` (${lastErrorCode})` : ''}
        </p>
      ) : null}

      {ordersQuery.data.length === 0 ? (
        <p className="py-4 text-center text-sm text-slate-500">
          No work orders yet for this company.
        </p>
      ) : (
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-slate-400">
                <th className="py-2.5 font-semibold">Order</th>
                <th className="py-2.5 font-semibold">Qty</th>
                <th className="py-2.5 font-semibold">Produced</th>
                <th className="py-2.5 font-semibold">Status</th>
                <th className="py-2.5 text-right font-semibold">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {ordersQuery.data.map((order) => (
                <tr key={order.id} className="h-11 hover:bg-slate-50">
                  <td className="py-2">
                    <button
                      type="button"
                      onClick={() => setSelectedId(order.id === selectedId ? null : order.id)}
                      className="font-mono font-medium text-sky-700 hover:underline"
                    >
                      {order.orderNumber}
                    </button>
                  </td>
                  <td className="py-2 font-mono">{order.quantityToProduce}</td>
                  <td className="py-2 font-mono">{order.producedQuantity}</td>
                  <td className="py-2">
                    <span
                      className={`rounded px-2 py-0.5 text-[11px] font-medium ${STATUS_BADGE[order.status]}`}
                    >
                      {order.status}
                    </span>
                  </td>
                  <td className="py-2">
                    <div className="flex justify-end gap-1.5">
                      {order.status === 'Draft' ? (
                        <ActionButton
                          label="Submit"
                          title="Submit for production"
                          pending={isSubmitting}
                          onClick={() => submit(order.id)}
                        />
                      ) : null}
                      {order.status === 'Submitted' ? (
                        <ActionButton
                          label="Transfer"
                          title="Transfer materials to WIP (MF-02)"
                          pending={isTransferring}
                          onClick={() => transfer(order.id)}
                        />
                      ) : null}
                      {order.status === 'InProcess' ? (
                        <ActionButton
                          label="Complete"
                          title="Complete manufacture (MF-03)"
                          pending={isCompleting}
                          onClick={() => {
                            setSelectedId(order.id)
                          }}
                        />
                      ) : null}
                      {order.status === 'Submitted' || order.status === 'InProcess' ? (
                        <ActionButton
                          label="Cancel"
                          title="Cancel work order (MF-05 reversal when issued)"
                          pending={isCancelling}
                          danger
                          onClick={() => cancel(order.id)}
                        />
                      ) : null}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {selected?.status === 'InProcess' ? (
        <div className="mt-4 rounded-lg border border-amber-200 bg-amber-50 p-4">
          <p className="flex items-center gap-1.5 text-xs font-semibold text-amber-900">
            <PackageCheck className="h-4 w-4" />
            Complete {selected.orderNumber} — produced quantity (max {selected.quantityToProduce})
          </p>
          <div className="mt-2 flex flex-wrap items-center gap-2">
            <input
              value={producedQty}
              onChange={(e) => setProducedQty(e.target.value)}
              placeholder={String(selected.quantityToProduce)}
              inputMode="decimal"
              className="w-32 rounded-lg border border-slate-300 bg-white px-3 py-1.5 font-mono text-xs"
            />
            <button
              type="button"
              disabled={isCompleting}
              onClick={() =>
                complete({
                  orderId: selected.id,
                  qty: producedQty === '' ? selected.quantityToProduce : Number(producedQty),
                })
              }
              className="flex items-center gap-1.5 rounded-lg bg-emerald-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-50"
            >
              <Truck className="h-3.5 w-3.5" />
              {isCompleting ? 'Posting…' : 'Post manufacture'}
            </button>
            {completeState.error ? (
              <span className="text-xs text-rose-700">
                {completeState.error}
                {completeState.errorCode ? ` (${completeState.errorCode})` : ''}
              </span>
            ) : null}
          </div>
        </div>
      ) : null}

      {selected && selected.status !== 'InProcess' ? (
        <p className="mt-4 flex items-center gap-1.5 text-xs text-slate-500">
          <ArrowRight className="h-3.5 w-3.5" />
          {selected.orderNumber}: {selected.producedQuantity} of {selected.quantityToProduce} produced
          {selected.actualStartDate ? ` · started ${selected.actualStartDate}` : ''}
          {selected.actualEndDate ? ` · ended ${selected.actualEndDate}` : ''}.
        </p>
      ) : null}
    </div>
  )
}

function ActionButton({
  label,
  title,
  pending,
  danger,
  onClick,
}: {
  label: string
  title: string
  pending: boolean
  danger?: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      title={title}
      disabled={pending}
      onClick={onClick}
      className={`rounded px-2.5 py-1 text-[11px] font-semibold disabled:opacity-50 ${
        danger
          ? 'border border-rose-300 text-rose-700 hover:bg-rose-50'
          : 'border border-slate-300 text-slate-700 hover:bg-slate-100'
      }`}
    >
      {label}
    </button>
  )
}
