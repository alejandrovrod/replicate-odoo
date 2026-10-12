import { useTranslation } from 'react-i18next'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table'
import { Pagination } from '../../../components/ui/Pagination'
import { Button } from '../../../components/ui/Button'
import { translateDocStatus } from '../../../lib/docStatus'
import type { SalesOrder } from '../api/useSalesOrders'

interface SalesOrderListProps {
  orders: SalesOrder[]
  totalCount: number
  page: number
  pageSize: number
  onPageChange: (page: number) => void
  onNew: () => void
  onSubmit: (order: SalesOrder) => void
  submittingId?: string | null
}

export function SalesOrderList({
  orders,
  totalCount,
  page,
  pageSize,
  onPageChange,
  onNew,
  onSubmit,
  submittingId,
}: SalesOrderListProps) {
  const { t, i18n } = useTranslation('selling')
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-semibold text-slate-900">{t('orders.title', 'Sales Orders')}</h3>
          <p className="text-xs text-slate-500">{t('orders.subtitle')}</p>
        </div>
        <Button size="sm" onClick={onNew}>
          {t('orders.new', 'New Order')}
        </Button>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white shadow-xs">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('orders.colOrder', 'Order No')}</TableHead>
              <TableHead>{t('orders.colCustomer', 'Customer')}</TableHead>
              <TableHead>{t('orders.colDelivery', 'Delivery')}</TableHead>
              <TableHead className="text-right">{t('orders.colTotal', 'Total')}</TableHead>
              <TableHead className="text-right">{t('orders.colDelivered', 'Delivered')}</TableHead>
              <TableHead className="text-right">{t('orders.colStatus', 'Status')}</TableHead>
              <TableHead className="text-right">{t('orders.colAction', 'Action')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {orders.length === 0 ? (
              <TableRow>
                <TableCell colSpan={7} className="h-24 text-center text-slate-500">
                  {t('orders.empty', 'No sales orders yet.')}
                </TableCell>
              </TableRow>
            ) : (
              orders.map((o) => (
                <TableRow key={o.id} className="hover:bg-slate-50">
                  <TableCell className="font-mono font-medium text-slate-900">{o.orderNumber}</TableCell>
                  <TableCell className="text-slate-800">{o.customerName || o.customerCode}</TableCell>
                  <TableCell className="font-mono text-slate-500">{o.deliveryDate}</TableCell>
                  <TableCell className="text-right font-mono font-bold text-slate-900">
                    {o.grandTotal.toLocaleString()}
                  </TableCell>
                  <TableCell className="text-right font-mono text-slate-600">
                    {o.deliveredPercentage.toFixed(2)}%
                  </TableCell>
                  <TableCell className="text-right">
                    <span className="rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-semibold text-slate-700">
                      {translateDocStatus(i18n, o.status)}
                    </span>
                  </TableCell>
                  <TableCell className="text-right">
                    {o.status === 'Draft' && (
                      <Button
                        size="sm"
                        variant="outline"
                        disabled={submittingId === o.id}
                        onClick={() => onSubmit(o)}
                      >
                        {submittingId === o.id ? t('orders.submitting', 'Submitting…') : t('orders.submit', 'Submit')}
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {totalCount > 0 && (
        <div className="flex justify-end">
          <Pagination totalCount={totalCount} page={page} pageSize={pageSize} onPageChange={onPageChange} />
        </div>
      )}
    </div>
  )
}
