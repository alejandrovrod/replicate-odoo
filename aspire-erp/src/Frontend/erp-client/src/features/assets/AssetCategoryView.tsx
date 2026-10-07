import { useState } from 'react'
import { MasterDataList, type ColumnDef } from '../../components/master-data/MasterDataList'
import { useAssetCategories, createAssetCategory, updateAssetCategory } from './api/useAssetCategories'
import type { AssetCategory } from './api/useAssetCategories'
import { AssetCategoryFormModal } from './components/AssetCategoryFormModal'
import { useTranslation } from 'react-i18next'
import { usePagination } from '../../lib/pagination'

export function AssetCategoryView() {
  const { t } = useTranslation('assets')
  
  const { page, pageSize, setPage } = usePagination(10)
  const { items, status, error, reload, totalCount, pageNumber } = useAssetCategories(page, pageSize)
  
  const [isModalOpen, setIsModalOpen] = useState(false)
  const [selectedCategory, setSelectedCategory] = useState<AssetCategory | null>(null)

  const columns: ColumnDef<AssetCategory>[] = [
    { header: t('categories.columns.name', 'Category Name'), accessor: 'categoryName' },
    {
      header: t('categories.columns.depreciable', 'Depreciable'),
      accessor: (row) => (
        <span className="text-sm">
          {row.isNonDepreciable ? t('categories.nonDepreciable', 'No') : t('categories.depreciable', 'Yes')}
        </span>
      ),
    },
    {
      header: t('categories.columns.status', 'Status'),
      accessor: (row) => (
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
            row.isActive ? 'bg-green-100 text-green-800' : 'bg-slate-100 text-slate-800'
          }`}
        >
          {row.isActive ? t('categories.active', 'Active') : t('categories.inactive', 'Disabled')}
        </span>
      ),
    },
  ]

  const handleSave = async (categoryData: Partial<AssetCategory>) => {
    if (selectedCategory?.id) {
      await updateAssetCategory(selectedCategory.id, categoryData)
    } else {
      await createAssetCategory(categoryData)
    }
    reload()
  }

  if (status === 'loading' && !items.length) return <div className="p-4">{t('categories.loading', 'Loading asset categories...')}</div>
  if (error) return <div className="p-4 text-red-600">{t('categories.error', 'Error loading categories:')} {error.message}</div>

  return (
    <div className="p-4">
      <MasterDataList
        title={t('categories.manageTitle', 'Asset Categories')}
        subtitle={t('categories.manageSubtitle', 'Manage your fixed asset classifications and default ledger accounts.')}
        newButtonText={t('categoryForm.titleNew', 'New Category')}
        searchPlaceholder={t('categories.search', 'Search asset categories...')}
        data={items}
        columns={columns}
        onNew={() => {
          setSelectedCategory(null)
          setIsModalOpen(true)
        }}
        onRowClick={(row) => {
          setSelectedCategory(row)
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

      <AssetCategoryFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        initialData={selectedCategory}
      />
    </div>
  )
}
