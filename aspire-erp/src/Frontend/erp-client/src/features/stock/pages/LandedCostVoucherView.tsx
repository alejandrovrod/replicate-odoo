import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../../api/client'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { useTenantStore } from '../../../store/useTenantStore'
import { usePagination } from '../../../lib/pagination'
import { Pagination } from '../../../components/ui/Pagination'
import { Button } from '../../../components/ui/Button'
import { FloatingActionButton } from '../../../components/ui/FloatingActionButton'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table'
import { translateDocStatus } from '../../../lib/docStatus'
import { LandedCostVoucherModal } from '../components/LandedCostVoucherModal'
import {
  cancelLandedCostVoucher,
  createLandedCostVoucher,
  submitLandedCostVoucher,
  useLandedCostVouchers,
  type CreateLandedCostPayload,
  type LandedCostVoucher,
} from '../api/useLandedCostVouchers'

function statusClass(status: string): string {
  if (status === 'Submitted') return 'bg-sky-100 text-sky-800'
  if (status === 'Cancelled') return 'bg-slate-200 text-slate-600'
  return 'bg-amber-100 text-amber-800'
}

/**
 * Landed cost voucher workbench (Fase 4, gap D5): Draft → Submitted (inventory uplift +
 * Kardex revaluation) → Cancelled by reversal. Drafts carry the server pre-calculation;
 * submit posts the balanced legs atomically.
 */
export function LandedCostVoucherView() {
  const { t, i18n } = useTranslation('stock')
  const companyId = useTenantStore((state) => state.companyId)
  const { page, pageSize, setPage } = usePagination(10)
  const { items, status, error, reload, totalCount, pageNumber } = useLandedCostVouchers(
    companyId,
    page,
    pageSize,
  )

  const [isModalOpen, setIsModalOpen] = useState(false)
  const [actingId, setActingId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const handleSave = async (data: CreateLandedCostPayload) => {
    await createLandedCostVoucher(data)
    reload()
  }

  const runAction = async (voucher: LandedCostVoucher, action: 'submit' | 'cancel') => {
    setActionError(null)
    setActingId(voucher.id)
    try {
      if (action === 'submit') {
        await submitLandedCostVoucher(voucher.id, companyId, voucher.rowVersion)
      } else {
        await cancelLandedCostVoucher(voucher.id, companyId, voucher.rowVersion)
      }
      reload()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setActionError(translateErrorCode(i18n, err.code, err.message))
      } else if (err instanceof Error) {
        setActionError(err.message)
      } else {
        setActionError(t('landedCost.actionError', 'The operation failed.'))
      }
    } finally {
      setActingId(null)
    }
  }

  if (status === 'loading' && items.length === 0)
    return <div className="p-4">{t('landedCost.loading', 'Loading landed cost vouchers…')}</div>
  if (error)
    return (
      <div className="p-4 text-red-600">
        {t('landedCost.loadError', 'Error loading vouchers: {{message}}', { message: error.message })}
      </div>
    )

  return (
    <div className="p-4 space-y-4">
      {actionError && <div className="text-sm text-red-600">{actionError}</div>}

      <div>
        <h3 className="text-base font-semibold text-slate-900">
          {t('landedCost.title', 'Landed Cost Vouchers')}
        </h3>
        <p className="text-xs text-slate-500">{t('landedCost.subtitle')}</p>
      </div>

      <FloatingActionButton
        label={t('landedCost.new', 'New Voucher')}
        onClick={() => setIsModalOpen(true)}
      />

      <div className="rounded-xl border border-slate-200 bg-white shadow-xs">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('landedCost.colVoucher', 'Voucher No')}</TableHead>
              <TableHead>{t('landedCost.colDate', 'Date')}</TableHead>
              <TableHead>{t('landedCost.colStrategy', 'Strategy')}</TableHead>
              <TableHead className="text-right">{t('landedCost.colCharges', 'Charges')}</TableHead>
              <TableHead className="text-right">{t('landedCost.colItems', 'Lines')}</TableHead>
              <TableHead className="text-right">{t('landedCost.colStatus', 'Status')}</TableHead>
              <TableHead className="text-right">{t('landedCost.colActions', 'Actions')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="h-24 text-center text-slate-500">
                  {t('landedCost.empty', 'No landed cost vouchers yet.')}
                </TableCell>
              </TableRow>
            ) : (
              items.map((voucher) => (
                <TableRow key={voucher.id} className="hover:bg-slate-50">
                  <TableCell className="font-mono font-medium text-slate-900">
                    {voucher.voucherNo || (
                      <span className="text-slate-400">{t('landedCost.draftBadge', '— draft —')}</span>
                    )}
                  </TableCell>
                  <TableCell className="font-mono text-slate-500">{voucher.postingDate}</TableCell>
                  <TableCell className="text-slate-800">
                    {voucher.distributeBy === 'Amount'
                      ? t('landedCost.strategyAmount', 'Amount (value)')
                      : t('landedCost.strategyQuantity', 'Quantity')}
                  </TableCell>
                  <TableCell className="text-right font-mono font-bold text-slate-900">
                    {voucher.totalTaxesAndCharges.toLocaleString()}
                  </TableCell>
                  <TableCell
                    className="text-right font-mono text-slate-600"
                    title={voucher.items
                      .map((l) => `${l.itemCode}: +${l.applicableCharges} → ${l.newRate}`)
                      .join('\n')}
                  >
                    {voucher.items.length}
                  </TableCell>
                  <TableCell className="text-right">
                    <span
                      className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${statusClass(voucher.status)}`}
                    >
                      {translateDocStatus(i18n, voucher.status)}
                    </span>
                  </TableCell>
                  <TableCell className="text-right">
                    <span className="inline-flex gap-1">
                      {voucher.status === 'Draft' && (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={actingId === voucher.id}
                          onClick={() => runAction(voucher, 'submit')}
                        >
                          {actingId === voucher.id
                            ? t('landedCost.working', '…')
                            : t('landedCost.submit', 'Submit')}
                        </Button>
                      )}
                      {voucher.status === 'Submitted' && (
                        <Button
                          size="sm"
                          variant="outline"
                          disabled={actingId === voucher.id}
                          onClick={() => runAction(voucher, 'cancel')}
                        >
                          {actingId === voucher.id
                            ? t('landedCost.working', '…')
                            : t('landedCost.cancelAction', 'Cancel')}
                        </Button>
                      )}
                    </span>
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {totalCount > 0 && (
        <div className="flex justify-end">
          <Pagination totalCount={totalCount} page={pageNumber} pageSize={pageSize} onPageChange={setPage} />
        </div>
      )}

      <LandedCostVoucherModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        companyId={companyId}
      />
    </div>
  )
}
