import { useState } from 'react'
import { MasterDataList, type ColumnDef } from '../../../components/master-data/MasterDataList'
import { useSuppliers, createSupplier, updateSupplier } from '../api/useSuppliers'
import type { Supplier } from '../api/useSuppliers'
import { SupplierFormModal } from '../components/SupplierFormModal'
import { useTranslation } from 'react-i18next'
import { usePagination } from '../../../lib/pagination'

export function SupplierView() {
  const { t } = useTranslation('buying')
  
  const { page, pageSize, setPage } = usePagination(10)
  const { items, status, error, reload, totalCount, pageNumber } = useSuppliers(page, pageSize)
  
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [selectedSupplier, setSelectedSupplier] = useState<Supplier | null>(null)

  const columns: ColumnDef<Supplier>[] = [
    { header: t('suppliers.columns.code', 'Code'), accessor: 'code' },
    { header: t('suppliers.columns.name', 'Name'), accessor: 'name' },
    { header: t('suppliers.columns.taxId', 'Tax ID'), accessor: 'taxId' },
    {
      header: t('suppliers.columns.status', 'Status'),
      accessor: (row) => (
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
            row.isActive ? 'bg-green-100 text-green-800' : 'bg-slate-100 text-slate-800'
          }`}
        >
          {row.isActive ? t('suppliers.active', 'Active') : t('suppliers.inactive', 'Disabled')}
        </span>
      ),
    },
  ]

  const handleSave = async (supplierData: Partial<Supplier>) => {
    if (selectedSupplier) {
      await updateSupplier(selectedSupplier.id, supplierData)
    } else {
      await createSupplier(supplierData)
    }
    reload()
  }

  if (status === 'loading' && !items.length) return <div className="p-4">Loading suppliers...</div>
  if (error) return <div className="p-4 text-red-600">Error loading suppliers: {error.message}</div>

  return (
    <div className="p-4">
      <MasterDataList
        title={t('suppliers.manageTitle', 'Suppliers')}
        subtitle={t('suppliers.manageSubtitle', 'Manage your suppliers and payment terms.')}
        newButtonText={t('supplierForm.titleNew', 'New Supplier')}
        searchPlaceholder={t('suppliers.search', 'Search suppliers...')}
        data={items}
        columns={columns}
        onNew={() => {
          setSelectedSupplier(null)
          setIsModalOpen(true)
        }}
        onRowClick={(row) => {
          setSelectedSupplier(row)
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

      <SupplierFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        initialData={selectedSupplier}
      />
    </div>
  )
}
