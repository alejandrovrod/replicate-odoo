import { useState } from 'react'
import { MasterDataList, type ColumnDef } from '../../components/master-data/MasterDataList'
import { useCurrencies, createCurrency, updateCurrency } from './api/useCurrencies'
import type { Currency } from './api/useCurrencies'
import { CurrencyFormModal } from './components/CurrencyFormModal'
import { useTranslation } from 'react-i18next'

export function CurrenciesView() {
  const { t } = useTranslation('accounting')
  const { items, status, error, reload } = useCurrencies(false)

  const [isModalOpen, setIsModalOpen] = useState(false)
  const [selectedCurrency, setSelectedCurrency] = useState<Currency | null>(null)

  const columns: ColumnDef<Currency>[] = [
    { header: t('currencies.columns.code', 'Code'), accessor: 'code' },
    { header: t('currencies.columns.symbol', 'Symbol'), accessor: 'symbol' },
    { header: t('currencies.columns.fraction', 'Fraction'), accessor: 'fractionName' },
    {
      header: t('currencies.columns.status', 'Status'),
      accessor: (row) => (
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
            row.isActive ? 'bg-green-100 text-green-800' : 'bg-slate-100 text-slate-800'
          }`}
        >
          {row.isActive ? t('currencies.active', 'Active') : t('currencies.inactive', 'Disabled')}
        </span>
      ),
    },
  ]

  const handleSave = async (currencyData: Partial<Currency>) => {
    if (selectedCurrency) {
      await updateCurrency(selectedCurrency.id, {
        code: currencyData.code!,
        symbol: currencyData.symbol!,
        fractionName: currencyData.fractionName,
        isActive: currencyData.isActive ?? true,
        rowVersion: currencyData.rowVersion,
      })
    } else {
      await createCurrency({
        code: currencyData.code!,
        symbol: currencyData.symbol!,
        fractionName: currencyData.fractionName,
        isActive: currencyData.isActive ?? true,
      })
    }
    reload()
  }

  if (status === 'loading' && !items.length) return <div className="p-4">Loading currencies...</div>
  if (error) return <div className="p-4 text-red-600">Error loading currencies: {error.message}</div>

  return (
    <div className="p-4">
      <MasterDataList
        title={t('currencies.manageTitle', 'Currencies')}
        subtitle={t('currencies.manageSubtitle', 'Global ISO catalog used by every monetary master.')}
        newButtonText={t('currencyForm.titleNew', 'New Currency')}
        searchPlaceholder={t('currencies.search', 'Search currencies...')}
        data={items}
        columns={columns}
        onNew={() => {
          setSelectedCurrency(null)
          setIsModalOpen(true)
        }}
        onRowClick={(row) => {
          setSelectedCurrency(row)
          setIsModalOpen(true)
        }}
      />

      <CurrencyFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        initialData={selectedCurrency}
      />
    </div>
  )
}
