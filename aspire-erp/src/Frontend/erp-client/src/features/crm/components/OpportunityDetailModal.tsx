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
import { Tabs, TabsContent, TabsList, TabsTrigger } from '../../../components/ui/Tabs'
import { ApiError } from '../../../api/client'
import { useErpAction } from '../../../lib/useErpAction'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { crmApi } from '../api/crmApi'
import { useCurrencies } from '../../accounting/api/useCurrencies'
import type {
  CrmCatalogDto,
  ItemOption,
  OpportunityDetailDto,
  OpportunityItemInput,
} from '../types/crm'

interface Props {
  /** Null closes the drawer; setting an id (re)loads the dossier. */
  opportunityId: string | null
  onClose: () => void
  /** The board reloads after a successful save (amounts/stages may move). */
  onSaved: () => void
}

interface LineDraft {
  key: string
  itemId: string
  quantity: number
  rate: number
  discountPercentage: number
}

const round2 = (value: number) => Math.round(value * 100) / 100

/**
 * Commercial dossier drawer (double-click a Kanban card): priced lines with reactive
 * totals, segmentation (territory/industry/segment/competitors) and the activity +
 * quotation history. Saves through PUT /api/v1/opportunities/{id} with the RowVersion
 * the dossier was read with - a concurrent edit surfaces as 409, never a silent
 * overwrite. Read-only once the deal leaves Open (advance/reopen own terminals).
 */
export function OpportunityDetailModal({ opportunityId, onClose, onSaved }: Props) {
  const { t, i18n } = useTranslation('crm')
  const [detail, setDetail] = useState<OpportunityDetailDto | null>(null)
  const [loading, setLoading] = useState(false)
  const [loadError, setLoadError] = useState<unknown>(null)

  const [partyName, setPartyName] = useState('')
  const [probability, setProbability] = useState(0)
  const [expectedClosingDate, setExpectedClosingDate] = useState('')
  const [currencyId, setCurrencyId] = useState('')
  const [exchangeRate, setExchangeRate] = useState('')
  const [territoryId, setTerritoryId] = useState('')
  const [industryId, setIndustryId] = useState('')
  const [marketSegment, setMarketSegment] = useState('')
  const [competitorsText, setCompetitorsText] = useState('')
  const [lines, setLines] = useState<LineDraft[]>([])

  const [items, setItems] = useState<ItemOption[]>([])
  const [territories, setTerritories] = useState<CrmCatalogDto[]>([])
  const [industries, setIndustries] = useState<CrmCatalogDto[]>([])
  const { items: currencies } = useCurrencies()

  useEffect(() => {
    if (!opportunityId) {
      setDetail(null)
      return
    }

    const controller = new AbortController()
    setLoading(true)
    setLoadError(null)
    Promise.all([
      crmApi.getOpportunityDetail(opportunityId, controller.signal),
      crmApi.getItems(controller.signal).catch((): ItemOption[] => []),
      crmApi.getTerritories(controller.signal).catch((): CrmCatalogDto[] => []),
      crmApi.getIndustries(controller.signal).catch((): CrmCatalogDto[] => []),
    ]).then(
      ([dossier, catalogItems, territoryRows, industryRows]) => {
        if (controller.signal.aborted) return
        setDetail(dossier)
        setPartyName(dossier.partyName)
        setProbability(dossier.probability)
        setExpectedClosingDate(dossier.expectedClosingDate)
        setCurrencyId(dossier.currencyId ?? '')
        setExchangeRate('')
        setTerritoryId(dossier.territoryId ?? '')
        setIndustryId(dossier.industryId ?? '')
        setMarketSegment(dossier.marketSegment ?? '')
        setCompetitorsText(dossier.competitors.join(', '))
        setLines(
          dossier.items.map((line, index) => ({
            key: `${line.id || index}`,
            itemId: line.itemId,
            quantity: line.quantity,
            rate: line.rate,
            discountPercentage: line.discountPercentage,
          })),
        )
        setItems(catalogItems)
        setTerritories(territoryRows)
        setIndustries(industryRows)
        setLoading(false)
      },
      (cause: unknown) => {
        if (controller.signal.aborted) return
        setLoadError(cause)
        setLoading(false)
      },
    )

    return () => controller.abort()
  }, [opportunityId])

  // Reactive preview mirrors the server engine: line nets sum to the header amount,
  // converted at the dossier rate for the base figure.
  const preview = useMemo(() => {
    const total = round2(
      lines.reduce(
        (sum, line) =>
          sum +
          (Number(line.quantity) || 0) *
            (Number(line.rate) || 0) *
            (1 - (Number(line.discountPercentage) || 0) / 100),
        0,
      ),
    )
    const rate = exchangeRate === '' ? (detail?.exchangeRate ?? 1) : Number(exchangeRate) || 0
    return { total, base: round2(total * rate) }
  }, [lines, exchangeRate, detail])

  const { state, dispatch, isPending } = useErpAction<OpportunityDetailDto, void>(async () => {
    if (!detail) throw new Error('No dossier loaded')
    const validLines: OpportunityItemInput[] = lines
      .filter((line) => line.itemId)
      .map((line) => ({
        itemId: line.itemId,
        quantity: Number(line.quantity) || 0,
        rate: Number(line.rate) || 0,
        discountPercentage: Number(line.discountPercentage) || 0,
      }))
    const saved = await crmApi.updateOpportunity({
      id: detail.id,
      rowVersion: detail.rowVersion,
      partyName,
      probability: Number(probability) || 0,
      expectedClosingDate: expectedClosingDate || undefined,
      currencyId: currencyId || null,
      exchangeRate: exchangeRate === '' ? undefined : Number(exchangeRate),
      territoryId: territoryId || null,
      industryId: industryId || null,
      marketSegment,
      competitors: competitorsText
        .split(',')
        .map((part) => part.trim())
        .filter((part) => part.length > 0),
      items: validLines,
    })
    setDetail(saved)
    onSaved()
    return saved
  })

  const updateLine = (key: string, patch: Partial<LineDraft>) => {
    setLines((prev) => prev.map((line) => (line.key === key ? { ...line, ...patch } : line)))
  }

  const readOnly = detail !== null && detail.status !== 'Open'

  return (
    <Dialog open={opportunityId !== null} onOpenChange={(next) => !next && onClose()}>
      <DialogContent className="max-w-3xl" aria-describedby={undefined}>
        <DialogHeader>
          <DialogTitle>
            {detail
              ? `${detail.opportunityNumber} — ${detail.partyName}`
              : t('detail.title', 'Opportunity dossier')}
          </DialogTitle>
        </DialogHeader>

        {loading && <p className="text-sm text-slate-500">{t('detail.loading', 'Loading dossier…')}</p>}
        {loadError ? (
          <p className="text-sm text-red-600">
            {loadError instanceof ApiError
              ? translateErrorCode(i18n, loadError.code, loadError.message)
              : t('detail.loadFailed', 'Failed to load the dossier.')}
          </p>
        ) : null}

        {detail && (
          <Tabs defaultValue="items" className="min-h-0 flex-1">
            <TabsList>
              <TabsTrigger value="items">{t('detail.tabs.items', 'Products & Services')}</TabsTrigger>
              <TabsTrigger value="segmentation">{t('detail.tabs.segmentation', 'Segmentation')}</TabsTrigger>
              <TabsTrigger value="history">{t('detail.tabs.history', 'Activities & History')}</TabsTrigger>
            </TabsList>

            <TabsContent value="items" className="space-y-3">
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <label className="block text-xs font-medium text-slate-600">
                  {t('detail.partyName', 'Customer / Party')}
                  <Input
                    value={partyName}
                    onChange={(e) => setPartyName(e.target.value)}
                    disabled={readOnly || isPending}
                    className="mt-1"
                  />
                </label>
                <label className="block text-xs font-medium text-slate-600">
                  {t('detail.probability', 'Probability %')}
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    value={probability}
                    onChange={(e) => setProbability(Number(e.target.value))}
                    disabled={readOnly || isPending}
                    className="mt-1"
                  />
                </label>
                <label className="block text-xs font-medium text-slate-600">
                  {t('detail.expectedClosing', 'Expected closing')}
                  <Input
                    type="date"
                    value={expectedClosingDate}
                    onChange={(e) => setExpectedClosingDate(e.target.value)}
                    disabled={readOnly || isPending}
                    className="mt-1"
                  />
                </label>
              </div>

              <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                <label className="block text-xs font-medium text-slate-600">
                  {t('detail.currency', 'Currency')}
                  <select
                    value={currencyId}
                    onChange={(e) => setCurrencyId(e.target.value)}
                    disabled={readOnly || isPending}
                    className="mt-1 flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                  >
                    <option value="">{t('detail.currencyDefault', 'USD (default)')}</option>
                    {currencies.map((currency) => (
                      <option key={currency.id} value={currency.id}>
                        {currency.code} — {currency.symbol}
                      </option>
                    ))}
                  </select>
                </label>
                <label className="block text-xs font-medium text-slate-600">
                  {t('detail.rate', 'Rate (empty = live rate)')}
                  <Input
                    type="number"
                    min={0}
                    step="0.000001"
                    value={exchangeRate}
                    onChange={(e) => setExchangeRate(e.target.value)}
                    placeholder={String(detail.exchangeRate)}
                    disabled={readOnly || isPending}
                    className="mt-1"
                  />
                </label>
              </div>

              <div className="flex items-center justify-between">
                <span className="text-sm font-medium text-slate-800">
                  {t('detail.lines', 'Lines')}
                </span>
                {!readOnly && (
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={() =>
                      setLines((prev) => [
                        ...prev,
                        {
                          key: `new-${Date.now()}-${prev.length}`,
                          itemId: '',
                          quantity: 1,
                          rate: 0,
                          discountPercentage: 0,
                        },
                      ])
                    }
                  >
                    {t('detail.addLine', 'Add line')}
                  </Button>
                )}
              </div>

              {lines.length === 0 && (
                <p className="text-xs text-slate-500">
                  {t('detail.noLines', 'Amount-only deal: the header amount stands alone.')}
                </p>
              )}
              {lines.map((line) => (
                <div key={line.key} className="grid grid-cols-12 items-center gap-2">
                  <select
                    value={line.itemId}
                    onChange={(e) => updateLine(line.key, { itemId: e.target.value })}
                    disabled={readOnly || isPending}
                    className="col-span-5 flex h-10 rounded-md border border-slate-200 bg-white px-2 py-2 text-sm"
                    aria-label={t('detail.lineItem', 'Item')}
                  >
                    <option value="">{t('detail.selectItem', '-- Item --')}</option>
                    {items.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.code} — {item.name}
                      </option>
                    ))}
                  </select>
                  <Input
                    type="number"
                    min={0}
                    step="0.0001"
                    value={line.quantity}
                    onChange={(e) => updateLine(line.key, { quantity: Number(e.target.value) })}
                    disabled={readOnly || isPending}
                    className="col-span-2"
                    aria-label={t('detail.lineQty', 'Qty')}
                  />
                  <Input
                    type="number"
                    min={0}
                    step="0.01"
                    value={line.rate}
                    onChange={(e) => updateLine(line.key, { rate: Number(e.target.value) })}
                    disabled={readOnly || isPending}
                    className="col-span-2"
                    aria-label={t('detail.lineRate', 'Rate')}
                  />
                  <Input
                    type="number"
                    min={0}
                    max={100}
                    step="0.01"
                    value={line.discountPercentage}
                    onChange={(e) => updateLine(line.key, { discountPercentage: Number(e.target.value) })}
                    disabled={readOnly || isPending}
                    className="col-span-2"
                    aria-label={t('detail.lineDiscount', 'Disc %')}
                  />
                  {!readOnly && (
                    <button
                      type="button"
                      onClick={() => setLines((prev) => prev.filter((l) => l.key !== line.key))}
                      className="col-span-1 text-xs text-red-600 hover:underline"
                      aria-label={t('detail.removeLine', 'Remove line')}
                    >
                      ✕
                    </button>
                  )}
                </div>
              ))}

              <div className="space-y-1 rounded-lg border border-slate-200 bg-white p-3 font-mono text-xs">
                <div className="flex justify-between text-slate-600">
                  <span>{t('detail.previewTotal', 'Lines total')}</span>
                  <span>
                    {preview.total.toLocaleString()} {detail.currencyCode}
                  </span>
                </div>
                <div className="flex justify-between text-slate-600">
                  <span>{t('detail.previewBase', 'Base (converted)')}</span>
                  <span>{preview.base.toLocaleString()}</span>
                </div>
                <div className="flex justify-between border-t border-slate-100 pt-1 text-sm font-bold text-slate-900">
                  <span>{t('detail.previewWeighted', 'Weighted')}</span>
                  <span>
                    {round2((preview.total * probability) / 100).toLocaleString()} {detail.currencyCode}
                  </span>
                </div>
              </div>
              {readOnly && (
                <p className="text-xs text-slate-500">
                  {t('detail.readOnly', 'Closed deals are read-only here; advance or re-open owns terminals.')}
                </p>
              )}
            </TabsContent>

            <TabsContent value="segmentation" className="grid grid-cols-1 gap-3 sm:grid-cols-2">
              <label className="block text-xs font-medium text-slate-600">
                {t('detail.territory', 'Territory')}
                <select
                  value={territoryId}
                  onChange={(e) => setTerritoryId(e.target.value)}
                  disabled={readOnly || isPending}
                  className="mt-1 flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                >
                  <option value="">{t('detail.none', '-- None --')}</option>
                  {territories.map((row) => (
                    <option key={row.id} value={row.id}>
                      {row.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block text-xs font-medium text-slate-600">
                {t('detail.industry', 'Industry')}
                <select
                  value={industryId}
                  onChange={(e) => setIndustryId(e.target.value)}
                  disabled={readOnly || isPending}
                  className="mt-1 flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm"
                >
                  <option value="">{t('detail.none', '-- None --')}</option>
                  {industries.map((row) => (
                    <option key={row.id} value={row.id}>
                      {row.name}
                    </option>
                  ))}
                </select>
              </label>
              <label className="block text-xs font-medium text-slate-600">
                {t('detail.marketSegment', 'Market segment')}
                <Input
                  value={marketSegment}
                  onChange={(e) => setMarketSegment(e.target.value)}
                  placeholder="SMB"
                  disabled={readOnly || isPending}
                  className="mt-1"
                />
              </label>
              <label className="block text-xs font-medium text-slate-600">
                {t('detail.competitors', 'Competitors (comma-separated)')}
                <Input
                  value={competitorsText}
                  onChange={(e) => setCompetitorsText(e.target.value)}
                  placeholder="Globex, Initech"
                  disabled={readOnly || isPending}
                  className="mt-1"
                />
              </label>
            </TabsContent>

            <TabsContent value="history" className="space-y-4">
              <div>
                <h4 className="mb-1 text-sm font-semibold text-slate-800">
                  {t('detail.activities', 'Activities')}
                </h4>
                {detail.activities.length === 0 && (
                  <p className="text-xs text-slate-500">{t('detail.noActivities', 'No activities yet.')}</p>
                )}
                <ul className="space-y-2">
                  {detail.activities.map((activity) => (
                    <li key={activity.id} className="rounded-md border border-slate-200 p-2">
                      <div className="flex items-center justify-between gap-2">
                        <span className="text-xs font-semibold text-slate-800">{activity.subject}</span>
                        <span className="font-mono text-[11px] text-slate-500">{activity.activityDate}</span>
                      </div>
                      {activity.content && (
                        <p className="mt-1 text-xs text-slate-600">{activity.content}</p>
                      )}
                    </li>
                  ))}
                </ul>
              </div>
              <div>
                <h4 className="mb-1 text-sm font-semibold text-slate-800">
                  {t('detail.quotations', 'Quotations')}
                </h4>
                {detail.quotations.length === 0 && (
                  <p className="text-xs text-slate-500">{t('detail.noQuotations', 'No quotations issued.')}</p>
                )}
                <ul className="space-y-2">
                  {detail.quotations.map((quotation) => (
                    <li
                      key={quotation.id}
                      className="flex items-center justify-between gap-2 rounded-md border border-slate-200 p-2 text-xs"
                    >
                      <span className="font-mono font-medium text-slate-900">{quotation.quotationNo}</span>
                      <span className="text-slate-500">{quotation.status}</span>
                      <span className="font-mono font-semibold text-slate-900">
                        {quotation.totalAmount.toLocaleString()}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
              {detail.lossReasonId && (
                <p className="text-xs text-slate-500">
                  {t('detail.lostReason', 'Lost reason')}: {detail.lossReasonName ?? detail.lossReasonId}
                  {detail.lossReason ? ` — ${detail.lossReason}` : ''}
                </p>
              )}
            </TabsContent>
          </Tabs>
        )}

        <DialogFooter className="border-t border-slate-100 pt-4">
          {state.error && (
            <p className="mr-auto text-xs text-red-600">
              {translateErrorCode(i18n, state.errorCode, state.error)}
            </p>
          )}
          <Button type="button" variant="outline" onClick={onClose}>
            {t('salesOrder.cancel', 'Cancel')}
          </Button>
          <Button
            type="button"
            disabled={!detail || readOnly || isPending || loading}
            onClick={() => dispatch()}
          >
            {isPending
              ? t('detail.saving', 'Saving…')
              : t('detail.save', 'Save dossier')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
