import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { getAssetDetail, type AssetDetail, capitalizeAsset } from './api/useAssets'
import { useTenantStore } from '../../store/useTenantStore'
import { Button } from '../../components/ui/Button'
import { ArrowLeft, Play, Settings2 } from 'lucide-react'

interface AssetDetailViewProps {
  assetId: string
  onBack: () => void
}

export function AssetDetailView({ assetId, onBack }: AssetDetailViewProps) {
  const { t } = useTranslation('assets')
  const companyId = useTenantStore((state) => state.companyId)
  
  const [detail, setDetail] = useState<AssetDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const [capitalizing, setCapitalizing] = useState(false)

  const fetchDetail = async () => {
    if (!companyId) return
    setLoading(true)
    setError(null)
    try {
      const data = await getAssetDetail(assetId, companyId)
      setDetail(data)
    } catch (err) {
      setError(err instanceof Error ? err : new Error('Failed to load asset details'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchDetail()
  }, [assetId, companyId])

  const handleCapitalize = async () => {
    if (!companyId || !detail) return
    setCapitalizing(true)
    try {
      await capitalizeAsset(assetId, companyId, detail.asset.availableForUseDate || detail.asset.purchaseDate)
      await fetchDetail()
    } catch (err) {
      alert(t('assets.capitalizeError', 'Failed to capitalize asset: ') + (err as Error).message)
    } finally {
      setCapitalizing(false)
    }
  }

  if (loading) return <div className="p-4">{t('assets.loading', 'Loading asset details...')}</div>
  if (error) return <div className="p-4 text-red-600">{t('assets.error', 'Error loading asset: ')} {error.message}</div>
  if (!detail) return null

  const { asset, schedule } = detail

  return (
    <div className="flex h-full flex-col bg-white">
      {/* Header */}
      <div className="flex items-center justify-between border-b border-slate-200 px-6 py-4">
        <div className="flex items-center gap-4">
          <button
            onClick={onBack}
            className="rounded-full p-2 text-slate-500 hover:bg-slate-100 hover:text-slate-900"
          >
            <ArrowLeft className="h-5 w-5" />
          </button>
          <div>
            <h1 className="text-xl font-semibold text-slate-900">{asset.assetName}</h1>
            <p className="text-sm text-slate-500">{asset.assetCode}</p>
          </div>
          <span className="ml-2 inline-flex items-center rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-slate-800">
            {asset.status}
          </span>
        </div>
        <div className="flex items-center gap-3">
          {asset.status === 'Draft' && (
            <Button onClick={handleCapitalize} disabled={capitalizing} className="flex items-center gap-2">
              <Play className="h-4 w-4" />
              {t('assets.capitalize', 'Capitalize Asset')}
            </Button>
          )}
        </div>
      </div>

      <div className="flex-1 overflow-auto p-6">
        <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
          
          {/* Asset Info Card */}
          <div className="col-span-1 rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="mb-4 flex items-center gap-2 text-base font-semibold text-slate-900">
              <Settings2 className="h-5 w-5 text-slate-500" />
              {t('assets.detailsTitle', 'Asset Details')}
            </h2>
            
            <dl className="space-y-4 text-sm">
              <div>
                <dt className="text-slate-500">{t('assets.fields.purchaseDate', 'Purchase Date')}</dt>
                <dd className="font-medium text-slate-900">{new Date(asset.purchaseDate).toLocaleDateString()}</dd>
              </div>
              <div>
                <dt className="text-slate-500">{t('assets.fields.grossPurchaseAmount', 'Gross Purchase Amount')}</dt>
                <dd className="font-medium text-slate-900">{asset.grossPurchaseAmount.toLocaleString()}</dd>
              </div>
              <div>
                <dt className="text-slate-500">{t('assets.fields.salvageValue', 'Salvage Value')}</dt>
                <dd className="font-medium text-slate-900">{asset.salvageValue.toLocaleString()}</dd>
              </div>
              <div>
                <dt className="text-slate-500">{t('assets.fields.depreciationMethod', 'Depreciation Method')}</dt>
                <dd className="font-medium text-slate-900">{asset.depreciationMethod}</dd>
              </div>
              <div>
                <dt className="text-slate-500">{t('assets.fields.nbv', 'Net Book Value')}</dt>
                <dd className="font-medium text-slate-900">{asset.netBookValue.toLocaleString()}</dd>
              </div>
              <div>
                <dt className="text-slate-500">{t('assets.fields.accumulatedDepreciation', 'Accumulated Depreciation')}</dt>
                <dd className="font-medium text-slate-900">{asset.accumulatedDepreciation.toLocaleString()}</dd>
              </div>
            </dl>
          </div>

          {/* Schedule Table */}
          <div className="col-span-1 lg:col-span-2 rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            <h2 className="mb-4 text-base font-semibold text-slate-900">
              {t('assets.scheduleTitle', 'Depreciation Schedule')}
            </h2>
            
            {schedule && schedule.length > 0 ? (
              <div className="overflow-x-auto">
                <table className="min-w-full divide-y divide-slate-200">
                  <thead>
                    <tr className="bg-slate-50 text-left text-xs font-semibold text-slate-500 uppercase tracking-wider">
                      <th className="px-4 py-3">{t('assets.schedule.date', 'Date')}</th>
                      <th className="px-4 py-3">{t('assets.schedule.amount', 'Amount')}</th>
                      <th className="px-4 py-3">{t('assets.schedule.accDep', 'Accumulated')}</th>
                      <th className="px-4 py-3">{t('assets.schedule.status', 'Status')}</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-200">
                    {schedule.map((line) => (
                      <tr key={line.id}>
                        <td className="px-4 py-3 text-sm text-slate-900">{new Date(line.scheduleDate).toLocaleDateString()}</td>
                        <td className="px-4 py-3 text-sm text-slate-900">{line.depreciationAmount.toLocaleString()}</td>
                        <td className="px-4 py-3 text-sm text-slate-900">{line.accumulatedDepreciationAfter.toLocaleString()}</td>
                        <td className="px-4 py-3 text-sm">
                          <span className={`inline-flex rounded-full px-2 py-0.5 text-xs font-medium ${
                            line.status === 'Booked' ? 'bg-emerald-100 text-emerald-800' :
                            line.status === 'Cancelled' ? 'bg-red-100 text-red-800' :
                            'bg-slate-100 text-slate-800'
                          }`}>
                            {line.status}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <p className="text-sm text-slate-500">{t('assets.noSchedule', 'No depreciation schedule found.')}</p>
            )}
          </div>

        </div>
      </div>
    </div>
  )
}
