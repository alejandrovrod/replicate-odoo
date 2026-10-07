import { useState } from 'react'
import { MasterDataList, type ColumnDef } from '../../../components/master-data/MasterDataList'
import { useCustomers, createCustomer, updateCustomer } from '../api/useCustomers'
import type { Customer } from '../api/useCustomers'
import { useTenantStore } from '../../../store/useTenantStore'
import { CustomerFormModal } from '../components/CustomerFormModal'
import { useTranslation } from 'react-i18next'
import { usePagination } from '../../../lib/pagination'

export function CustomerView() {
  const { t } = useTranslation('selling')
  const companyId = useTenantStore((state) => state.companyId)
  
  const { page, pageSize, setPage } = usePagination(10)
  const { items, status, error, reload, totalCount, pageNumber } = useCustomers(companyId, page, pageSize)
  
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null)

  const columns: ColumnDef<Customer>[] = [
    { header: t('customers.columns.code', 'Code'), accessor: 'customerCode' },
    { header: t('customers.columns.name', 'Name'), accessor: 'name' },
    { header: t('customers.columns.taxId', 'Tax ID'), accessor: 'taxId' },
    {
      header: t('customers.columns.creditLimit', 'Credit Limit'),
      accessor: (row) => row.creditLimit?.toLocaleString() || '-',
    },
    {
      header: t('customers.columns.status', 'Status'),
      accessor: (row) => (
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
            row.isActive ? 'bg-green-100 text-green-800' : 'bg-slate-100 text-slate-800'
          }`}
        >
          {row.isActive ? t('customers.active', 'Active') : t('customers.inactive', 'Disabled')}
        </span>
      ),
    },
  ]

  const handleSave = async (customerData: Partial<Customer>) => {
    if (selectedCustomer) {
      await updateCustomer(selectedCustomer.id, customerData)
    } else {
      await createCustomer(customerData)
    }
    reload()
  }

  if (status === 'loading' && !items.length) return <div className="p-4">Loading customers...</div>
  if (error) return <div className="p-4 text-red-600">Error loading customers: {error.message}</div>

  return (
    <div className="p-4">
      <MasterDataList
        title={t('customers.manageTitle', 'Customers')}
        subtitle={t('customers.manageSubtitle', 'Manage your customers and credit limits.')}
        newButtonText={t('customerForm.titleNew', 'New Customer')}
        searchPlaceholder={t('customers.search', 'Search customers...')}
        data={items}
        columns={columns}
        onNew={() => {
          setSelectedCustomer(null)
          setIsModalOpen(true)
        }}
        onRowClick={(row) => {
          setSelectedCustomer(row)
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

      <CustomerFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        initialData={selectedCustomer}
        companyId={companyId}
      />
    </div>
  )
}
