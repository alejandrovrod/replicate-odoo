import { useState } from 'react'
import { FileText, Truck, AlertCircle } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import { Pagination } from '../../components/ui/Pagination'
import { useApiList } from '../../lib/useApiList'
import { MAX_PAGE_SIZE, usePagination } from '../../lib/pagination'
import { formatMoney } from '../../lib/format'
import { useTenantStore } from '../../store/useTenantStore'
import { PurchaseReceiptModal } from './PurchaseReceiptModal'

interface PurchaseOrder {
  id: string
  orderNumber: string
  supplierId: string
  supplierName: string
  transactionDate: string
  grandTotal: number
  status: string
  billedPercentage: number
  receivedPercentage: number
  items: {
    itemId: string
    itemName: string
    quantity: number
    receivedQuantity: number
    rate: number
  }[]
}

export function BuyingOverview() {
  const { t } = useTranslation('buying')
  const companyId = useTenantStore((state) => state.companyId)
  const paging = usePagination()
  const enabled = Boolean(companyId)

  // Table page (pager below) + ONE bounded read for the unbilled stat (no pager).
  const ordersQuery = useApiList<PurchaseOrder>(
    '/v1/purchaseorders',
    { companyId, page: paging.page, pageSize: paging.pageSize },
    enabled,
  )
  const statsQuery = useApiList<PurchaseOrder>(
    '/v1/purchaseorders',
    { companyId, page: 1, pageSize: MAX_PAGE_SIZE },
    enabled,
  )
  const orders = ordersQuery.status === 'success' ? ordersQuery.items : []
  const isLoading = ordersQuery.status === 'loading'
  const error = ordersQuery.status === 'error' ? ordersQuery.error : null

  const [selectedOrder, setSelectedOrder] = useState<PurchaseOrder | null>(null)

  const reloadAll = () => {
    ordersQuery.reload()
    statsQuery.reload()
  }

  // Vendor aging metric over the bounded stat read (see note above).
  const unbilledAmount = (statsQuery.status === 'success' ? statsQuery.items : [])
    .filter(o => o.receivedPercentage > 0 && o.billedPercentage < 100)
    .reduce((sum, o) => sum + (o.grandTotal * (1 - (o.billedPercentage / 100))), 0)
    
  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 rounded-xl border border-indigo-100 bg-indigo-50/50 p-6 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-lg font-bold text-slate-900">{t('overview.title')}</h2>
          <p className="mt-1 text-xs text-slate-600">{t('overview.subtitle')}</p>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            className="flex items-center gap-1.5 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs font-semibold text-slate-700 shadow-xs hover:bg-slate-50"
          >
            <Truck className="h-4 w-4 text-indigo-600" />
            {t('overview.newReceipt')}
          </button>
          <button
            type="button"
            className="flex items-center gap-1.5 rounded-lg bg-indigo-600 px-3 py-2 text-xs font-semibold text-white shadow-xs hover:bg-indigo-700"
          >
            <FileText className="h-4 w-4" />
            {t('overview.newOrder')}
          </button>
        </div>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-3">
        <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
          <h3 className="text-sm font-medium text-slate-500">{t('stats.unbilled')}</h3>
          <p className="mt-2 text-3xl font-bold text-slate-900">{formatMoney(unbilledAmount)}</p>
        </div>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white p-6 shadow-xs">
        <h3 className="text-base font-semibold text-slate-900">{t('orders.title')}</h3>
        <p className="text-xs text-slate-500 mb-4">{t('orders.subtitle')}</p>

        {error ? (
          <div className="mb-4 flex items-center gap-2 rounded-lg bg-red-50 p-3 text-sm text-red-600">
            <AlertCircle className="h-4 w-4" />
            {error.status === 0 ? t('orders.loadFailed') : error.message}
          </div>
        ) : null}

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead>
              <tr className="border-b border-slate-200 text-slate-400">
                <th className="py-2.5 font-semibold">{t('orders.colNumber')}</th>
                <th className="py-2.5 font-semibold">{t('orders.colVendor')}</th>
                <th className="py-2.5 font-semibold">{t('orders.colDate')}</th>
                <th className="py-2.5 font-semibold text-right">{t('orders.colAmount')}</th>
                <th className="py-2.5 font-semibold text-right">{t('orders.colStatus')}</th>
                <th className="py-2.5 font-semibold text-right">{t('orders.colActions')}</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {isLoading ? (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-slate-500">{t('orders.loading')}</td>
                </tr>
              ) : orders.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-slate-500">{t('orders.empty')}</td>
                </tr>
              ) : (
                orders.map((po) => (
                  <tr key={po.id} className="hover:bg-slate-50">
                    <td className="py-3 font-mono font-medium text-slate-900">{po.orderNumber}</td>
                    <td className="py-3 text-slate-800">{po.supplierName}</td>
                    <td className="py-3 font-mono text-slate-500">{po.transactionDate}</td>
                    <td className="py-3 text-right font-mono font-bold text-slate-900">
                      {formatMoney(po.grandTotal)}
                    </td>
                    <td className="py-3 text-right">
                      <span
                        className={`rounded-full px-2 py-0.5 text-[10px] font-semibold ${
                          po.status === 'Completed'
                            ? 'bg-emerald-100 text-emerald-800'
                            : po.status === 'PartiallyReceived'
                              ? 'bg-amber-100 text-amber-800'
                              : po.status === 'Draft'
                                ? 'bg-slate-100 text-slate-800'
                                : 'bg-sky-100 text-sky-800'
                        }`}
                      >
                        {po.status}
                      </span>
                    </td>
                    <td className="py-3 text-right">
                      {po.status === 'Submitted' || po.status === 'PartiallyReceived' ? (
                        <button
                          onClick={() => setSelectedOrder(po)}
                          className="rounded text-indigo-600 hover:text-indigo-800 font-semibold"
                        >
                          {t('orders.receive')}
                        </button>
                      ) : null}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        <div className="mt-3 flex justify-end">
          <Pagination
            totalCount={ordersQuery.totalCount}
            page={paging.page}
            pageSize={paging.pageSize}
            onPageChange={paging.setPage}
          />
        </div>
      </div>

      {selectedOrder && (
        <PurchaseReceiptModal
          order={selectedOrder}
          onClose={() => setSelectedOrder(null)}
          onSuccess={reloadAll}
        />
      )}
    </div>
  )
}
