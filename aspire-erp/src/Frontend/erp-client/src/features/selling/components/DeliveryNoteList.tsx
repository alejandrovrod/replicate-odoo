import { useTranslation } from 'react-i18next'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table'
import { Pagination } from '../../../components/ui/Pagination'
import { Button } from '../../../components/ui/Button'
import type { DeliveryNote } from '../api/useDeliveryNotes'

interface DeliveryNoteListProps {
  notes: DeliveryNote[]
  totalCount: number
  page: number
  pageSize: number
  onPageChange: (page: number) => void
  onNew: () => void
}

export function DeliveryNoteList({ notes, totalCount, page, pageSize, onPageChange, onNew }: DeliveryNoteListProps) {
  const { t } = useTranslation('selling')
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-base font-semibold text-slate-900">{t('deliveries.title', 'Delivery Notes')}</h3>
          <p className="text-xs text-slate-500">{t('deliveries.subtitle')}</p>
        </div>
        <Button size="sm" onClick={onNew}>
          {t('deliveries.new', 'New Delivery')}
        </Button>
      </div>

      <div className="rounded-xl border border-slate-200 bg-white shadow-xs">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{t('deliveries.colVoucher', 'Voucher No')}</TableHead>
              <TableHead>{t('deliveries.colDate', 'Posting Date')}</TableHead>
              <TableHead className="text-right">{t('deliveries.colLines', 'Lines')}</TableHead>
              <TableHead className="text-right">{t('deliveries.colQty', 'Total Qty')}</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {notes.length === 0 ? (
              <TableRow>
                <TableCell colSpan={4} className="h-24 text-center text-slate-500">
                  {t('deliveries.empty', 'No delivery notes yet.')}
                </TableCell>
              </TableRow>
            ) : (
              notes.map((n) => (
                <TableRow key={n.id} className="hover:bg-slate-50">
                  <TableCell className="font-mono font-medium text-slate-900">{n.voucherNo}</TableCell>
                  <TableCell className="font-mono text-slate-500">{n.postingDate}</TableCell>
                  <TableCell className="text-right font-mono text-slate-600">{n.lines.length}</TableCell>
                  <TableCell className="text-right font-mono font-bold text-slate-900">
                    {n.lines.reduce((sum, l) => sum + l.qty, 0).toLocaleString()}
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
