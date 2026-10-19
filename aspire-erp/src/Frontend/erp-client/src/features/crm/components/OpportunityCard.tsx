import { useTranslation } from 'react-i18next'
import type { OpportunityDto } from '../types/crm';

interface Props {
  opportunity: OpportunityDto;
  /** Double-click (or Enter) opens the commercial dossier drawer. */
  onOpenDetail?: (opportunity: OpportunityDto) => void;
}

export function OpportunityCard({ opportunity, onOpenDetail }: Props) {
  const { t } = useTranslation('crm')
  return (
    <div
      className="bg-white p-4 rounded-lg shadow-sm border border-gray-200 mb-3 cursor-grab active:cursor-grabbing hover:shadow-md transition-shadow"
      onDoubleClick={() => onOpenDetail?.(opportunity)}
      onKeyDown={(e) => {
        if ((e.key === 'Enter' || e.key === ' ') && onOpenDetail) {
          e.preventDefault()
          onOpenDetail(opportunity)
        }
      }}
      tabIndex={onOpenDetail ? 0 : undefined}
      role={onOpenDetail ? 'button' : undefined}
      aria-label={onOpenDetail ? `${opportunity.opportunityNumber} — ${opportunity.partyName}` : undefined}
    >
      <div className="flex justify-between items-start mb-2">
        <span className="text-xs font-semibold text-gray-500">{opportunity.opportunityNumber}</span>
        <span className="text-xs font-medium px-2 py-1 bg-blue-50 text-blue-700 rounded-full">
          {opportunity.probability}%
        </span>
      </div>
      <h4 className="font-medium text-gray-900 mb-1">{opportunity.partyName}</h4>
      <div className="flex justify-between items-end mt-4">
        <div>
          <p className="text-xs text-gray-500">{t('card.value')}</p>
          <p className="font-medium text-gray-900">${opportunity.opportunityAmount.toLocaleString()}</p>
        </div>
        <div className="text-right">
          <p className="text-xs text-gray-500">{t('card.weighted')}</p>
          <p className="font-medium text-emerald-600">${opportunity.weightedAmount.toLocaleString()}</p>
        </div>
      </div>
    </div>
  );
}
