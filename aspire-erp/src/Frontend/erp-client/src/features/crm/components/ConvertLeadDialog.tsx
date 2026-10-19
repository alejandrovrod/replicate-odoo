import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { Input } from '../../../components/ui/Input'
import { useErpAction } from '../../../lib/useErpAction'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { crmApi } from '../api/crmApi'
import type { ConvertLeadResult, LeadDto } from '../types/crm'

interface Props {
  /** Non-null opens the dialog and drives the prospect summary (null closes it). */
  lead: LeadDto | null
  onClose: () => void
  /** Fired after the server created the customer + opportunity (spec CRM-03). */
  onConverted: (result: ConvertLeadResult) => void
}

interface ConvertDraft {
  customerCode: string
  opportunityAmount: number
  opportunityProbability: number
  expectedClosingDate: string
  paymentTermsDays: number
}

const DEFAULT_PROBABILITY = 25
const DEFAULT_PAYMENT_TERMS_DAYS = 30

/** Random 6-char suffix for the `CLI-…` fallback when no company name is available. */
function randomCodeSuffix(): string {
  return Math.random().toString(36).slice(2, 8).toUpperCase()
}

/**
 * Suggests a customer code from the prospect's company name (uppercased, non-alphanumerics
 * collapsed to `-`) and falls back to a random `CLI-XXXXXX` when there is no company to
 * derive it from. Pure and exported so the two branches can be pinned by a unit test.
 */
export function suggestCustomerCode(lead: Pick<LeadDto, 'organizationName'>): string {
  const slug = (lead.organizationName ?? '')
    .trim()
    .toUpperCase()
    .replace(/[^A-Z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 30)
  return slug.length > 0 ? slug : `CLI-${randomCodeSuffix()}`
}

function initialDraft(lead: LeadDto | null): ConvertDraft {
  return {
    customerCode: lead ? suggestCustomerCode(lead) : '',
    opportunityAmount: 0,
    opportunityProbability: DEFAULT_PROBABILITY,
    expectedClosingDate: '',
    paymentTermsDays: DEFAULT_PAYMENT_TERMS_DAYS,
  }
}

/**
 * Lead-to-opportunity conversion (spec CRM-03): confirms the prospect, collects the customer
 * code (pre-suggested) plus the opportunity terms, and calls the convert route. RFC 7807
 * rejections (duplicate customer code 409, unknown lead 404…) surface through `useErpAction`;
 * on success the parent closes the dialog and reloads the pipeline.
 */
export function ConvertLeadDialog({ lead, onClose, onConverted }: Props) {
  const { t, i18n } = useTranslation('crm')
  const [draft, setDraft] = useState<ConvertDraft>(() => initialDraft(lead))

  // Re-seed the form whenever a different prospect is targeted.
  useEffect(() => {
    if (lead) setDraft(initialDraft(lead))
  }, [lead])

  const { state, dispatch, isPending } = useErpAction<ConvertLeadResult, ConvertDraft>(async (payload) => {
    if (!lead) throw new Error('No lead selected')
    const result = await crmApi.convertLead(lead.id, {
      customerCode: payload.customerCode.trim(),
      opportunityAmount: Number(payload.opportunityAmount) || 0,
      opportunityProbability: Number(payload.opportunityProbability) || 0,
      expectedClosingDate: payload.expectedClosingDate || null,
      paymentTermsDays: Number(payload.paymentTermsDays) || 0,
    })
    onConverted(result)
    return result
  })

  const update = (patch: Partial<ConvertDraft>) => setDraft((prev) => ({ ...prev, ...patch }))

  return (
    <Dialog open={lead !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="max-w-lg" aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>{t('leads.convertTitle', 'Convert to Opportunity')}</DialogTitle>
          <DialogDescription>
            {t(
              'leads.convertDescription',
              'Creates a customer and an opportunity at the Qualification stage (25%).',
            )}
          </DialogDescription>
        </DialogHeader>

        {lead && (
          <>
            <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
              <p className="text-sm font-semibold text-slate-900">{lead.leadName}</p>
              <p className="text-xs text-slate-600">{lead.organizationName ?? '—'}</p>
            </div>

            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <div className="sm:col-span-2">
                <label className="block text-xs font-medium text-slate-600">
                  {t('leads.customerCode', 'Customer code')}
                  <Input
                    value={draft.customerCode}
                    onChange={(e) => update({ customerCode: e.target.value })}
                    disabled={isPending}
                    required
                    className="mt-1"
                  />
                </label>
                <span className="mt-1 block text-[11px] text-slate-400">
                  {t('leads.customerCodeHint', 'Suggested from the company name — edit if needed.')}
                </span>
              </div>
              <label className="block text-xs font-medium text-slate-600">
                {t('leads.amount', 'Opportunity amount')}
                <Input
                  type="number"
                  min={0}
                  step="0.01"
                  value={draft.opportunityAmount}
                  onChange={(e) => update({ opportunityAmount: Number(e.target.value) })}
                  disabled={isPending}
                  className="mt-1"
                />
              </label>
              <label className="block text-xs font-medium text-slate-600">
                {t('leads.probability', 'Probability %')}
                <Input
                  type="number"
                  min={0}
                  max={100}
                  value={draft.opportunityProbability}
                  onChange={(e) => update({ opportunityProbability: Number(e.target.value) })}
                  disabled={isPending}
                  className="mt-1"
                />
              </label>
              <label className="block text-xs font-medium text-slate-600">
                {t('leads.expectedClosing', 'Expected closing')}
                <Input
                  type="date"
                  value={draft.expectedClosingDate}
                  onChange={(e) => update({ expectedClosingDate: e.target.value })}
                  disabled={isPending}
                  className="mt-1"
                />
              </label>
              <label className="block text-xs font-medium text-slate-600">
                {t('leads.paymentTerms', 'Payment terms (days)')}
                <Input
                  type="number"
                  min={0}
                  value={draft.paymentTermsDays}
                  onChange={(e) => update({ paymentTermsDays: Number(e.target.value) })}
                  disabled={isPending}
                  className="mt-1"
                />
              </label>
            </div>
          </>
        )}

        <DialogFooter className="border-t border-slate-100 pt-4">
          {state.error && (
            <p className="mr-auto text-xs text-red-600">
              {translateErrorCode(i18n, state.errorCode, state.error)}
            </p>
          )}
          <Button type="button" variant="outline" onClick={onClose}>
            {t('leads.cancel', 'Cancel')}
          </Button>
          <Button
            type="button"
            disabled={!lead || isPending || draft.customerCode.trim().length === 0}
            onClick={() => dispatch(draft)}
          >
            {isPending
              ? t('leads.convertSubmitting', 'Converting…')
              : t('leads.convertSubmit', 'Convert')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
