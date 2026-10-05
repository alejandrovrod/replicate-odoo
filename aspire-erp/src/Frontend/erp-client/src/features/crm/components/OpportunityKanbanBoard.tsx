import { useCallback, useEffect, useMemo, useState } from 'react';
import type { AdvanceStagePayload, OpportunityDto, OpportunityStage } from '../types/crm';
import { OpportunityCard } from './OpportunityCard';
import { CreateSalesOrderForm } from './CreateSalesOrderForm';
import { ApiError } from '../../../api/client';
import { useErpAction } from '../../../lib/useErpAction';
import { crmApi } from '../api/crmApi';

const STAGES: OpportunityStage[] = ['Prospecting', 'Qualification', 'Proposal', 'Negotiation', 'ClosedWon', 'ClosedLost'];

export function OpportunityKanbanBoard() {
  const [opportunities, setOpportunities] = useState<OpportunityDto[]>([]);

  const reload = useCallback(() => {
    crmApi.getOpportunities().then(setOpportunities).catch(console.error);
  }, []);

  useEffect(() => {
    reload();
  }, [reload]);

  // Stage updates go through the live advance endpoint (POST
  // /api/v1/opportunities/{id}/advance); useErpAction unwraps RFC 7807 rejections
  // (400 missing loss reason, 409 concurrency conflict) into state.error. The merge
  // and the conflict reload live INSIDE this callback (the WorkOrdersBoard precedent),
  // never in an effect on the action state.
  const { state, dispatch, isPending } = useErpAction<OpportunityDto, AdvanceStagePayload>(
    async (payload) => {
      try {
        const updated = await crmApi.advanceStage(payload);
        setOpportunities(prev => prev.map(o => o.id === updated.id ? updated : o));
        return updated;
      } catch (err) {
        // A 409 concurrency_conflict means another tab moved the deal first, so reload
        // the board instead of keeping the optimistic row.
        if (err instanceof ApiError && err.code === 'concurrency_conflict') {
          reload();
        }
        throw err;
      }
    }
  );

  const handleDrop = (id: string, newStage: OpportunityStage) => {
    // If dragging into same stage, do nothing
    const opp = opportunities.find(o => o.id === id);
    if (!opp || opp.stage === newStage) return;

    if (newStage === 'ClosedLost') {
      const reason = window.prompt("Please provide a loss reason (Mandatory for ClosedLost):");
      if (!reason) return; // Invariant CRM-02 guard at UI level
      dispatch({ id, toStage: newStage, lossReason: reason, rowVersion: opp.rowVersion });
    } else {
      dispatch({ id, toStage: newStage, rowVersion: opp.rowVersion });
    }
  };

  const handleDragStart = (e: React.DragEvent, id: string) => {
    e.dataTransfer.setData('opportunityId', id);
  };

  // Weighted forecast (Invariant CRM-01 UI projection): sum of amount x probability.
  const totalWeighted = useMemo(() =>
    opportunities.reduce((acc, curr) => acc + curr.weightedAmount, 0),
  [opportunities]);

  return (
    <div className="p-6 h-full flex flex-col">
      <div className="mb-6 flex justify-between items-center bg-white p-4 rounded-xl shadow-sm border border-gray-100">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">Sales Pipeline</h2>
          <p className="text-gray-500">Drag and drop opportunities across stages</p>
        </div>
        <div className="text-right">
          <p className="text-sm font-medium text-gray-500">Total Weighted Forecast</p>
          <p className="text-3xl font-bold text-emerald-600">
            ${totalWeighted.toLocaleString(undefined, { minimumFractionDigits: 2 })}
          </p>
        </div>
      </div>

      <div className="flex gap-4 overflow-x-auto pb-4 flex-1">
        {STAGES.map(stage => (
          <div
            key={stage}
            className={`flex-shrink-0 w-80 bg-gray-50 rounded-xl flex flex-col ${isPending ? 'opacity-75' : ''}`}
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => handleDrop(e.dataTransfer.getData('opportunityId'), stage)}
          >
            <div className="p-4 border-b border-gray-200 flex justify-between items-center">
              <h3 className="font-semibold text-gray-700">{stage}</h3>
              <span className="bg-gray-200 text-gray-600 px-2 py-1 rounded-full text-xs font-medium">
                {opportunities.filter(o => o.stage === stage).length}
              </span>
            </div>
            <div className="p-4 flex-1 overflow-y-auto">
              {opportunities.filter(o => o.stage === stage).map(opp => (
                <div key={opp.id} draggable onDragStart={(e) => handleDragStart(e, opp.id)}>
                  <OpportunityCard opportunity={opp} />
                  {opp.status === 'Won' && (
                    <CreateSalesOrderForm opportunity={opp} onCreated={reload} />
                  )}
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>

      {state.error && (
        <div className="fixed bottom-4 right-4 bg-red-100 text-red-700 p-4 rounded-lg shadow-lg border border-red-200">
          <p className="font-bold">Error updating stage</p>
          <p className="text-sm">{state.error}</p>
        </div>
      )}
    </div>
  );
}
