import React from 'react';
import { OpportunityDto } from '../types/crm';

interface Props {
  opportunity: OpportunityDto;
}

export function OpportunityCard({ opportunity }: Props) {
  return (
    <div className="bg-white p-4 rounded-lg shadow-sm border border-gray-200 mb-3 cursor-grab active:cursor-grabbing hover:shadow-md transition-shadow">
      <div className="flex justify-between items-start mb-2">
        <span className="text-xs font-semibold text-gray-500">{opportunity.opportunityNumber}</span>
        <span className="text-xs font-medium px-2 py-1 bg-blue-50 text-blue-700 rounded-full">
          {opportunity.probability}%
        </span>
      </div>
      <h4 className="font-medium text-gray-900 mb-1">{opportunity.partyName}</h4>
      <div className="flex justify-between items-end mt-4">
        <div>
          <p className="text-xs text-gray-500">Value</p>
          <p className="font-medium text-gray-900">${opportunity.opportunityAmount.toLocaleString()}</p>
        </div>
        <div className="text-right">
          <p className="text-xs text-gray-500">Weighted</p>
          <p className="font-medium text-emerald-600">${opportunity.weightedAmount.toLocaleString()}</p>
        </div>
      </div>
    </div>
  );
}
