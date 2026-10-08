import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { usePeriodClosing } from '../api/usePeriodClosing'
import { Button } from '../../../components/ui/Button'
import { PeriodClosingFormModal } from './PeriodClosingFormModal'

export function PeriodClosingList() {
  const { t } = useTranslation('accounting')
  const { items, status, reload } = usePeriodClosing()
  const [isModalOpen, setIsModalOpen] = useState(false)

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold tracking-tight text-slate-900">
          {t('periodClosing.title', 'Period Closing Vouchers')}
        </h1>
        <Button onClick={() => setIsModalOpen(true)}>
          {t('periodClosing.newBtn', 'New Closing Voucher')}
        </Button>
      </div>

      <div className="rounded-lg border border-slate-200 bg-white shadow-sm">
        <table className="w-full text-left text-sm text-slate-600">
          <thead className="border-b border-slate-200 bg-slate-50 text-slate-900">
            <tr>
              <th className="px-6 py-4 font-medium">{t('field.voucherNo', 'Voucher No')}</th>
              <th className="px-6 py-4 font-medium">{t('field.postingDate', 'Posting Date')}</th>
              <th className="px-6 py-4 font-medium">{t('field.status', 'Status')}</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200">
            {status === 'success' && items.map((item) => (
              <tr key={item.id} className="hover:bg-slate-50">
                <td className="px-6 py-4 font-medium text-sky-600">{item.voucherNo}</td>
                <td className="px-6 py-4">{item.postingDate.split('T')[0]}</td>
                <td className="px-6 py-4">{item.documentStatus}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      
      <PeriodClosingFormModal 
        isOpen={isModalOpen} 
        onClose={() => setIsModalOpen(false)}
        onSaved={reload}
      />
    </div>
  )
}
