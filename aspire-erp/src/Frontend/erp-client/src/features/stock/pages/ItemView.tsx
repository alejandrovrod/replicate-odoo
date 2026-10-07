import { useState } from 'react'
import { MasterDataList, type ColumnDef } from '../../../components/master-data/MasterDataList'
import { useItems, createItem, updateItem } from '../useStockData'
import type { Item } from '../types'
import { useTenantStore } from '../../../store/useTenantStore'
import { ItemFormModal } from '../components/ItemFormModal'
import { useTranslation } from 'react-i18next'
import { usePagination } from '../../../lib/pagination'

export function ItemView() {
  const { t } = useTranslation('stock')
  const companyId = useTenantStore((state) => state.companyId)
  
  const { page, pageSize, setPage } = usePagination(10)
  const { items, status, error, reload, totalCount, pageNumber } = useItems(companyId, page, pageSize)
  
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [selectedItem, setSelectedItem] = useState<Item | null>(null)

  const columns: ColumnDef<Item>[] = [
    { header: t('items.columns.code', 'Code'), accessor: 'code' },
    { header: t('items.columns.name', 'Name'), accessor: 'name' },
    { header: t('items.columns.uom', 'Base UOM'), accessor: 'baseUOMId' }, // TODO: resolve name
    { header: t('items.columns.valuation', 'Valuation'), accessor: 'valuationMethod' },
    {
      header: t('items.columns.status', 'Status'),
      accessor: (row) => (
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
            row.isActive ? 'bg-green-100 text-green-800' : 'bg-slate-100 text-slate-800'
          }`}
        >
          {row.isActive ? t('items.active', 'Active') : t('items.inactive', 'Disabled')}
        </span>
      ),
    },
  ]

  const handleSave = async (itemData: Partial<Item>) => {
    if (selectedItem) {
      await updateItem(selectedItem.id, itemData)
    } else {
      await createItem(itemData)
    }
    reload()
  }

  if (status === 'loading' && !items.length) return <div className="p-4">Loading items...</div>
  if (error) return <div className="p-4 text-red-600">Error loading items: {error.message}</div>

  return (
    <div className="p-4">
      <MasterDataList
        title={t('items.manageTitle', 'Items & Products')}
        subtitle={t('items.manageSubtitle', 'Manage SKUs, products, and services.')}
        newButtonText={t('itemForm.titleNew', 'New Item')}
        searchPlaceholder={t('items.search', 'Search items...')}
        data={items}
        columns={columns}
        onNew={() => {
          setSelectedItem(null)
          setIsModalOpen(true)
        }}
        onRowClick={(row) => {
          setSelectedItem(row)
          setIsModalOpen(true)
        }}
        pagination={
          totalCount > 0
            ? {
                totalCount: totalCount,
                page: pageNumber,
                pageSize: pageSize,
                onPageChange: setPage,
              }
            : undefined
        }
      />

      <ItemFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        initialData={selectedItem}
        companyId={companyId}
      />
    </div>
  )
}
