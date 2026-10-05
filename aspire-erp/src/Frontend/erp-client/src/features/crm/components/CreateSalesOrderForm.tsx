import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../../api/client'
import { useErpAction } from '../../../lib/useErpAction'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { crmApi } from '../api/crmApi'
import type { CreateSalesOrderPayload, ItemOption, OpportunityDto, SalesOrderCreated } from '../types/crm'

interface Props {
  opportunity: OpportunityDto
  onCreated: () => void
}

/**
 * 1-click sales-order creation for a ClosedWon deal (spec CRM-02, fix-pass C2).
 * A sales order needs LINES while a deal carries only an amount, so the form takes the
 * single line explicitly: the item is picked from the catalog, quantity defaults to 1 and
 * the rate defaults to the deal amount (server totals then equal the opportunity value).
 * Items load lazily on first open (a user gesture, never an effect).
 */
export function CreateSalesOrderForm({ opportunity, onCreated }: Props) {
  const { t, i18n } = useTranslation('crm')
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState<ItemOption[]>([])
  // Raw error, not a pre-rendered string: the generic branch has to re-translate when the
  // language changes (see the same decision in CrmOverview).
  const [itemsError, setItemsError] = useState<unknown>(null)
  const [itemId, setItemId] = useState('')
  const [quantity, setQuantity] = useState('1')
  const [rate, setRate] = useState(String(opportunity.opportunityAmount))

  const { state, dispatch, isPending } = useErpAction<SalesOrderCreated, CreateSalesOrderPayload>(
    async (payload) => {
      const created = await crmApi.createSalesOrder(payload)
      onCreated()
      return created
    },
  )

  const handleOpen = () => {
    if (!open && items.length === 0 && !itemsError) {
      crmApi.getItems().then(
        (rows) => {
          setItems(rows)
          if (rows.length > 0) setItemId(rows[0].id)
        },
        (cause: unknown) => {
          setItemsError(cause)
        },
      )
    }
    setOpen(!open)
  }

  const handleSubmit = () => {
    dispatch({
      id: opportunity.id,
      itemId,
      quantity: Number(quantity),
      rate: Number(rate),
    })
  }

  if (!open) {
    return (
      <button
        type="button"
        onClick={handleOpen}
        className="mt-2 w-full rounded-md border border-emerald-300 bg-emerald-50 px-2 py-1 text-xs font-semibold text-emerald-700 hover:bg-emerald-100"
      >
        {t('salesOrder.create')}
      </button>
    )
  }

  return (
    <div className="mt-2 rounded-md border border-emerald-200 bg-emerald-50/50 p-2">
      {state.data ? (
        <p className="text-xs font-medium text-emerald-700">
          {t('salesOrder.created', { orderNumber: state.data.orderNumber })}
        </p>
      ) : (
        <>
          {itemsError ? (
            <p className="text-xs text-red-600">
              {itemsError instanceof ApiError ? itemsError.message : t('salesOrder.loadItemsFailed')}
            </p>
          ) : (
            <>
              <label className="mb-1 block text-[11px] font-medium text-gray-500">
                {t('salesOrder.item')}
                <select
                  value={itemId}
                  onChange={(e) => setItemId(e.target.value)}
                  className="mt-0.5 block w-full rounded border border-gray-300 bg-white px-1 py-1 text-xs text-gray-900"
                >
                  {items.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.code} — {item.name}
                    </option>
                  ))}
                </select>
              </label>
              <div className="flex gap-2">
                <label className="block flex-1 text-[11px] font-medium text-gray-500">
                  {t('salesOrder.qty')}
                  <input
                    value={quantity}
                    onChange={(e) => setQuantity(e.target.value)}
                    inputMode="decimal"
                    className="mt-0.5 block w-full rounded border border-gray-300 bg-white px-1 py-1 text-xs text-gray-900"
                  />
                </label>
                <label className="block flex-1 text-[11px] font-medium text-gray-500">
                  {t('salesOrder.rate')}
                  <input
                    value={rate}
                    onChange={(e) => setRate(e.target.value)}
                    inputMode="decimal"
                    className="mt-0.5 block w-full rounded border border-gray-300 bg-white px-1 py-1 text-xs text-gray-900"
                  />
                </label>
              </div>
              {state.error ? (
                <p className="mt-1 text-xs text-red-600">
                  {translateErrorCode(i18n, state.errorCode, state.error)}
                </p>
              ) : null}
              <div className="mt-2 flex gap-2">
                <button
                  type="button"
                  onClick={handleSubmit}
                  disabled={isPending || !itemId}
                  className="flex-1 rounded-md bg-emerald-600 px-2 py-1 text-xs font-semibold text-white hover:bg-emerald-700 disabled:opacity-50"
                >
                  {isPending ? t('salesOrder.creating') : t('salesOrder.createOrder')}
                </button>
                <button
                  type="button"
                  onClick={() => setOpen(false)}
                  className="rounded-md border border-gray-300 bg-white px-2 py-1 text-xs text-gray-600 hover:bg-gray-50"
                >
                  {t('salesOrder.cancel')}
                </button>
              </div>
            </>
          )}
        </>
      )}
    </div>
  )
}
