import { useState } from 'react'
import { MasterDataList, type ColumnDef } from '../../../components/master-data/MasterDataList'
import { useSuppliers, createSupplier, updateSupplier } from '../api/useSuppliers'
import type { Supplier } from '../api/useSuppliers'
import { SupplierFormModal } from '../components/SupplierFormModal'
import type { SupplierFormPayload } from '../components/SupplierFormModal'
import { useSupplierGroups } from '../api/useSupplierGroups'
import { useTranslation } from 'react-i18next'
import { usePagination } from '../../../lib/pagination'

export function SupplierView() {
  const { t } = useTranslation('buying')

  const { page, pageSize, setPage, reset } = usePagination(10)
  const [groupId, setGroupId] = useState('')
  const { items, status, error, reload, totalCount, pageNumber } = useSuppliers(page, pageSize, {
    groupId: groupId || undefined,
  })
  const groupsQuery = useSupplierGroups()

  const [isModalOpen, setIsModalOpen] = useState(false)
  const [selectedSupplier, setSelectedSupplier] = useState<Supplier | null>(null)

  const holdTypeLabel = (supplier: Supplier) => {
    if (supplier.holdType === 'Invoices') return t('supplierForm.holdTypeInvoices', 'Invoices')
    if (supplier.holdType === 'Payments') return t('supplierForm.holdTypePayments', 'Payments')
    return t('supplierForm.holdTypeAll', 'All documents')
  }

  const renderStatus = (row: Supplier) => {
    // Priority: a disabled supplier always reads as disabled; a hold is more actionable than a
    // record-level freeze, so it wins when both are set.
    const [label, badgeClass] = !row.isActive
      ? [t('suppliers.inactive', 'Disabled'), 'bg-slate-100 text-slate-800']
      : row.onHold
        ? [`${t('suppliers.onHold', 'On Hold')} · ${holdTypeLabel(row)}`, 'bg-amber-100 text-amber-800']
        : row.isFrozen
          ? [t('suppliers.frozen', 'Frozen'), 'bg-sky-100 text-sky-800']
          : [t('suppliers.active', 'Active'), 'bg-green-100 text-green-800']

    return (
      <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${badgeClass}`}>
        {label}
      </span>
    )
  }

  const columns: ColumnDef<Supplier>[] = [
    { header: t('suppliers.columns.code', 'Code'), accessor: 'code' },
    { header: t('suppliers.columns.name', 'Name'), accessor: 'name' },
    { header: t('suppliers.columns.taxId', 'Tax ID'), accessor: 'taxId' },
    {
      header: t('suppliers.columns.group', 'Group'),
      accessor: (row) => row.supplierGroupName || '—',
    },
    {
      header: t('suppliers.columns.status', 'Status'),
      accessor: renderStatus,
    },
  ]

  const handleSave = async (supplierData: SupplierFormPayload) => {
    if (selectedSupplier) {
      await updateSupplier(selectedSupplier.id, supplierData)
    } else {
      await createSupplier(supplierData)
    }
    reload()
  }

  const groupFilter = (
    <select
      value={groupId}
      onChange={(e) => {
        setGroupId(e.target.value)
        // A filter change can strand the user on an empty page: jump back to page one.
        reset()
      }}
      disabled={groupsQuery.status !== 'success'}
      className="flex h-10 rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
    >
      <option value="">{t('suppliers.allGroups', 'All groups')}</option>
      {groupsQuery.status === 'success' && groupsQuery.items.map((group) => (
        <option key={group.id} value={group.id}>
          {group.code} — {group.name}
        </option>
      ))}
    </select>
  )

  if (status === 'loading' && !items.length) return <div className="p-4">{t('suppliers.loading', 'Loading suppliers...')}</div>
  if (error) return <div className="p-4 text-red-600">{t('suppliers.loadError', 'Error loading suppliers: {{message}}', { message: error.message })}</div>

  return (
    <div className="p-4">
      <MasterDataList
        title={t('suppliers.manageTitle', 'Suppliers')}
        subtitle={t('suppliers.manageSubtitle', 'Manage your suppliers and payment terms.')}
        newButtonText={t('supplierForm.titleNew', 'New Supplier')}
        searchPlaceholder={t('suppliers.search', 'Search suppliers...')}
        data={items}
        columns={columns}
        filters={groupFilter}
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
