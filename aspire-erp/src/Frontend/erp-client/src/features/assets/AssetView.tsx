import { useState } from 'react'
import { MasterDataList, type ColumnDef } from '../../components/master-data/MasterDataList'
import { useAssets, type Asset } from './api/useAssets'
import { AssetDetailView } from './AssetDetailView'
import { RunDepreciationModal } from './components/RunDepreciationModal'
import { useTranslation } from 'react-i18next'
import { Button } from '../../components/ui/Button'
import { Calculator } from 'lucide-react'
import { usePagination } from '../../lib/pagination'

/** Status badge label keys (explicit map: template-literal keys are not type-safe). */
const ASSET_STATUS_KEY = {
  Draft: 'assets.status.draft',
  Capitalized: 'assets.status.capitalized',
  Sold: 'assets.status.sold',
  Scrapped: 'assets.status.scrapped',
} as const

export function AssetView() {
  const { t } = useTranslation('assets')
  const { page, pageSize, setPage } = usePagination(10)
  const { items, status, error, totalCount, pageNumber, reload } = useAssets(page, pageSize)
  
  const [selectedAsset, setSelectedAsset] = useState<Asset | null>(null)
  const [isDepreciationModalOpen, setIsDepreciationModalOpen] = useState(false)

  const columns: ColumnDef<Asset>[] = [
    { header: t('assets.columns.code', 'Asset Code'), accessor: 'assetCode' },
    { header: t('assets.columns.name', 'Asset Name'), accessor: 'assetName' },
    {
      header: t('assets.columns.purchaseDate', 'Purchase Date'),
      accessor: (row) => new Date(row.purchaseDate).toLocaleDateString()
    },
    {
      header: t('assets.columns.purchaseAmount', 'Purchase Amount'),
      accessor: (row) => row.grossPurchaseAmount.toLocaleString(undefined, { minimumFractionDigits: 2 })
    },
    {
      header: t('assets.columns.nbv', 'Net Book Value'),
      accessor: (row) => row.netBookValue.toLocaleString(undefined, { minimumFractionDigits: 2 })
    },
    {
      header: t('assets.columns.status', 'Status'),
      accessor: (row) => {
        let colorClass = 'bg-slate-100 text-slate-800'
        if (row.status === 'Capitalized') colorClass = 'bg-blue-100 text-blue-800'
        if (row.status === 'Draft') colorClass = 'bg-amber-100 text-amber-800'
        if (row.status === 'Sold' || row.status === 'Scrapped') colorClass = 'bg-slate-200 text-slate-600'
        
        return (
          <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${colorClass}`}>
            {t(ASSET_STATUS_KEY[row.status], row.status)}
          </span>
        )
      },
    },
  ]

  if (status === 'loading' && !items.length) return <div className="p-4">{t('assets.loading', 'Loading assets...')}</div>
  if (error) return <div className="p-4 text-red-600">{t('assets.error', 'Error loading assets:')} {error.message}</div>

  if (selectedAsset) {
    return <AssetDetailView assetId={selectedAsset.id} onBack={() => setSelectedAsset(null)} />
  }

  return (
    <div className="p-4">
      <MasterDataList
        title={t('assets.manageTitle', 'Fixed Assets')}
        subtitle={t('assets.manageSubtitle', 'Manage your fixed assets, view net book value, and track depreciation.')}
        newButtonText={t('assets.titleNew', 'New Asset')}
        searchPlaceholder={t('assets.search', 'Search assets...')}
        actions={
          <Button variant="secondary" onClick={() => setIsDepreciationModalOpen(true)} className="flex items-center gap-2">
            <Calculator className="h-4 w-4" />
            {t('assets.depreciation.runBtn', 'Run Depreciation')}
          </Button>
        }
        data={items}
        columns={columns}
        onNew={() => {
          // Typically assets are created via purchasing module or DB seeds as per design.
          // We can show a toast or a modal explaining this.
          alert(t('assets.creationNotice', 'Assets are automatically created when capitalized from the purchasing module or initialized via data seeds.'))
        }}
        onRowClick={(row) => {
          setSelectedAsset(row)
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

      <RunDepreciationModal
        isOpen={isDepreciationModalOpen}
        onClose={() => setIsDepreciationModalOpen(false)}
        onSuccess={() => {
          reload()
        }}
      />
    </div>
  )
}
