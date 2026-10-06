import { useState } from 'react'
import { MasterDataList, type ColumnDef } from '../../../components/master-data/MasterDataList'
import { useWarehouses, type Warehouse } from '../api/useWarehouses'
import { useTenantStore } from '../../../store/useTenantStore'
import { WarehouseFormModal } from '../components/WarehouseFormModal'
import { useTranslation } from 'react-i18next'
import { ChevronDown, ChevronRight } from 'lucide-react'

export function WarehouseView() {
  const { t } = useTranslation('stock')
  const companyId = useTenantStore((state) => state.companyId)
  const { data, loading, error, createWarehouse, updateWarehouse } = useWarehouses(companyId)
  
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [selectedWarehouse, setSelectedWarehouse] = useState<Warehouse | null>(null)
  const [expandedRows, setExpandedRows] = useState<Record<string, boolean>>({})

  const toggleRow = (id: string, e: React.MouseEvent) => {
    e.stopPropagation()
    setExpandedRows((prev) => ({ ...prev, [id]: prev[id] === undefined ? false : !prev[id] }))
  }

  // A flattened approach to display the tree structure
  const flattenTree = (nodes: any[], depth = 0): any[] => {
    let result: any[] = []
    for (const node of nodes) {
      result.push({ ...node, depth })
      const isExpanded = expandedRows[node.id] !== false // Default to expanded
      if (node.children && node.children.length > 0 && isExpanded) {
        result = result.concat(flattenTree(node.children, depth + 1))
      }
    }
    return result
  }

  const flattenedData = flattenTree(data)
  const groupWarehouses = flattenedData.filter(wh => wh.isGroup)

  const columns: ColumnDef<any>[] = [
    {
      header: t('warehouses.columns.code', 'Code'),
      accessor: (row) => {
        const isExpanded = expandedRows[row.id] !== false
        return (
          <div style={{ paddingLeft: `${row.depth * 1.5}rem` }} className="flex items-center gap-2">
            {row.isGroup ? (
              <button
                type="button"
                onClick={(e) => toggleRow(row.id, e)}
                className="flex h-5 w-5 items-center justify-center rounded hover:bg-slate-200 text-slate-500"
              >
                {isExpanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
              </button>
            ) : (
              <div className="h-5 w-5" /> // placeholder for alignment
            )}
            <span className={row.isGroup ? 'font-medium text-slate-900' : 'text-slate-700'}>
              {row.code}
            </span>
          </div>
        )
      },
    },
    { header: t('warehouses.columns.name', 'Name'), accessor: 'name' },
    { 
      header: t('warehouses.columns.type', 'Type'), 
      accessor: (row) => (row.isGroup ? t('warehouses.typeGroup', 'Group') : t('warehouses.typeLedger', 'Ledger')) 
    },
    {
      header: t('warehouses.columns.status', 'Status'),
      accessor: (row) => (
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
            row.isActive ? 'bg-green-100 text-green-800' : 'bg-slate-100 text-slate-800'
          }`}
        >
          {row.isActive ? t('warehouses.active', 'Active') : t('warehouses.inactive', 'Disabled')}
        </span>
      ),
    },
  ]

  const handleSave = async (warehouseData: Partial<Warehouse>) => {
    if (selectedWarehouse) {
      await updateWarehouse(selectedWarehouse.id, warehouseData)
    } else {
      await createWarehouse(warehouseData)
    }
  }

  if (loading) return <div className="p-4">Loading warehouses...</div>
  if (error) return <div className="p-4 text-red-600">Error loading warehouses: {error.message}</div>

  return (
    <div className="p-4">
      <MasterDataList
        title={t('warehouses.manageTitle', 'Warehouses')}
        subtitle={t('warehouses.manageSubtitle', "Manage your company's warehouse locations and hierarchical groups.")}
        newButtonText={t('warehouseForm.titleNew', 'New')}
        searchPlaceholder={t('warehouses.search', 'Search...')}
        data={flattenedData}
        columns={columns}
        onNew={() => {
          setSelectedWarehouse(null)
          setIsModalOpen(true)
        }}
        onRowClick={(row) => {
          setSelectedWarehouse(row)
          setIsModalOpen(true)
        }}
      />

      <WarehouseFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        initialData={selectedWarehouse}
        companyId={companyId}
        groupWarehouses={groupWarehouses}
      />
    </div>
  )
}
