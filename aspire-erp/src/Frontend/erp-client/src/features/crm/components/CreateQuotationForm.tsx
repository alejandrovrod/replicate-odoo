import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useErpAction } from '../../../lib/useErpAction'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { crmApi } from '../api/crmApi'
import type { OpportunityDto, QuotationDto } from '../types/crm'

interface Props {
  opportunity: OpportunityDto
  onCreated: () => void
}

/**
 * 1-click quotation issue for a ClosedWon deal (standard enterprise pre-order
 * flow): lines drag automatically from the deal's priced breakdown, validity defaults
 * to 30 days server-side. Amount-only deals reject with `crm_quotation_requires_items`
 * and the error tells the user to price lines in the detail drawer first.
 */
export function CreateQuotationForm({ opportunity, onCreated }: Props) {
  const { t, i18n } = useTranslation('crm')
  const [open, setOpen] = useState(false)
  const [validTill, setValidTill] = useState('')

  const { state, dispatch, isPending } = useErpAction<QuotationDto, { id: string }>(
    async (payload) => {
      const created = await crmApi.createQuotation({
        id: payload.id,
        validTill: validTill || null,
      })
      onCreated()
      return created
    },
  )

  if (!open) {
    return (
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="mt-2 w-full rounded-md bg-sky-600 px-2 py-1 text-xs font-semibold text-white hover:bg-sky-700"
      >
        {t('quotation.create', 'Create Quotation')}
      </button>
    )
  }

  return (
    <div className="mt-2 rounded-md border border-sky-200 bg-sky-50/50 p-2">
      {state.data ? (
        <p className="text-xs font-medium text-sky-700">
          {t('quotation.created', {
            defaultValue: 'Quotation {{quotationNo}} issued.',
            quotationNo: state.data.quotationNo,
          })}
        </p>
      ) : (
        <>
          <label className="mb-1 block text-[11px] font-medium text-gray-500">
            {t('quotation.validTill', 'Valid until (optional)')}
            <input
              type="date"
              value={validTill}
              onChange={(e) => setValidTill(e.target.value)}
              className="mt-0.5 block w-full rounded border border-gray-300 bg-white px-1 py-1 text-xs text-gray-900"
            />
          </label>
          {state.error ? (
            <p className="mt-1 text-xs text-red-600">
              {translateErrorCode(i18n, state.errorCode, state.error)}
            </p>
          ) : null}
          <div className="mt-2 flex gap-2">
            <button
              type="button"
              onClick={() => dispatch({ id: opportunity.id })}
              disabled={isPending}
              className="flex-1 rounded-md bg-sky-600 px-2 py-1 text-xs font-semibold text-white hover:bg-sky-700 disabled:opacity-50"
            >
              {isPending
                ? t('quotation.issuing', 'Issuing…')
                : t('quotation.issue', 'Issue quotation')}
            </button>
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="rounded-md border border-gray-300 bg-white px-2 py-1 text-xs text-gray-600 hover:bg-gray-50"
            >
              {t('salesOrder.cancel', 'Cancel')}
            </button>
          </div>
        </>
      )}
    </div>
  )
}
