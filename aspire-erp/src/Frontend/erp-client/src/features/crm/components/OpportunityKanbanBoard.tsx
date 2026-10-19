import { useCallback, useEffect, useMemo, useOptimistic, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { MAX_PAGE_SIZE } from '../../../lib/pagination';
import type { AdvanceStagePayload, CrmCatalogDto, OpportunityDto, OpportunityStage } from '../types/crm';
import { OpportunityCard } from './OpportunityCard';
import { CreateSalesOrderForm } from './CreateSalesOrderForm';
import { CreateQuotationForm } from './CreateQuotationForm';
import { LossReasonDialog } from './LossReasonDialog';
import { OpportunityDetailModal } from './OpportunityDetailModal';
import { ApiError } from '../../../api/client';
import { useErpAction } from '../../../lib/useErpAction';
import { translateErrorCode } from '../../../lib/translateErrorCode';
import { crmApi } from '../api/crmApi';

const STAGES: OpportunityStage[] = ['Prospecting', 'Qualification', 'Proposal', 'Negotiation', 'ClosedWon', 'ClosedLost'];

interface PendingLoss {
  id: string
  rowVersion?: string
}

interface Props {
  /** Bumped by the overview after a lead conversion so the board re-reads the pipeline. */
  reloadToken?: number
}

export function OpportunityKanbanBoard({ reloadToken = 0 }: Props) {
  const { t, i18n } = useTranslation('crm');
  const [opportunities, setOpportunities] = useState<OpportunityDto[]>([]);
  const [pendingLoss, setPendingLoss] = useState<PendingLoss | null>(null);
  const [lossReasons, setLossReasons] = useState<CrmCatalogDto[]>([]);
  const [loadingReasons, setLoadingReasons] = useState(false);
  const [reasonsError, setReasonsError] = useState<string | null>(null);
  const [detailId, setDetailId] = useState<string | null>(null);

  // Kanban exception to pagination: board groups by stage client-side, needs full
  // context on one bounded page (MAX_PAGE_SIZE, no pager).
  const reload = useCallback(() => {
    crmApi.getOpportunities(undefined, 1, MAX_PAGE_SIZE).then((page) => setOpportunities(page.items)).catch(console.error);
  }, []);

  useEffect(() => { reload(); }, [reload, reloadToken]);

  // Optimistic drag-drop (React 19 useOptimistic): the card jumps to the target column
  // instantly; the server answer below either confirms (base state merge) or reverts
  // (full reload on 409, which also refreshes the stale RowVersion).
  const [optimisticOpportunities, applyOptimisticMove] = useOptimistic(
    opportunities,
    (prev, move: { id: string; stage: OpportunityStage }) =>
      prev.map((o) => (o.id === move.id ? { ...o, stage: move.stage } : o)),
  );

  // Stage updates via POST /api/v1/opportunities/{id}/advance; useErpAction unwraps
  // RFC 7807 rejections (400 missing loss reason, 409 concurrency conflict) into state.error.
  const { state, dispatch, isPending } = useErpAction<OpportunityDto, AdvanceStagePayload>(
    async (payload) => {
      try {
        const updated = await crmApi.advanceStage(payload);
        setOpportunities(prev => prev.map(o => o.id === updated.id ? updated : o));
        return updated;
      } catch (err) {
        if (err instanceof ApiError && err.code === 'concurrency_conflict') { reload(); }
        throw err;
      }
    }
  );

  const advance = (id: string, newStage: OpportunityStage, extra?: Partial<AdvanceStagePayload>) => {
    const opp = opportunities.find(o => o.id === id);
    if (!opp || opp.stage === newStage) return;
    applyOptimisticMove({ id, stage: newStage });
    dispatch({ id, toStage: newStage, rowVersion: opp.rowVersion, ...extra });
  };

  const handleDrop = (id: string, newStage: OpportunityStage) => {
    if (newStage === 'ClosedLost') {
      const opp = opportunities.find(o => o.id === id);
      if (!opp || opp.stage === newStage) return;
      // Accessible dialog (replaces window.prompt): catalog reason + notes.
      setLoadingReasons(true);
      setReasonsError(null);
      crmApi.getLossReasons().then(
        (rows) => {
          setLossReasons(rows);
          setLoadingReasons(false);
          setPendingLoss({ id, rowVersion: opp.rowVersion });
        },
        (cause: unknown) => {
          setLoadingReasons(false);
          setReasonsError(
            cause instanceof ApiError ? cause.message : t('lossReason.loadFailed'),
          );
          setPendingLoss({ id, rowVersion: opp.rowVersion });
        },
      );
      return;
    }
    advance(id, newStage);
  };

  const confirmLoss = (lossReasonId: string, notes: string) => {
    if (!pendingLoss) return;
    const { id, rowVersion } = pendingLoss;
    setPendingLoss(null);
    const opp = opportunities.find(o => o.id === id);
    applyOptimisticMove({ id, stage: 'ClosedLost' });
    dispatch({
      id,
      toStage: 'ClosedLost',
      lossReasonId,
      lossReason: notes || null,
      rowVersion: rowVersion ?? opp?.rowVersion,
    });
  };

  const handleDragStart = (e: React.DragEvent, id: string) => {
    e.dataTransfer.setData('opportunityId', id);
  };

  const totalWeighted = useMemo(() =>
    optimisticOpportunities.reduce((acc, curr) => acc + curr.weightedAmount, 0),
  [optimisticOpportunities]);

  return (
    <div className="p-6 h-full flex flex-col">
      <div className="mb-6 flex justify-between items-center bg-white p-4 rounded-xl shadow-sm border border-gray-100">
        <div>
          <h2 className="text-2xl font-bold text-gray-900">{t('pipeline.title')}</h2>
          <p className="text-gray-500">{t('pipeline.dragHint')}</p>
        </div>
        <div className="text-right">
          <p className="text-sm font-medium text-gray-500">{t('pipeline.weightedForecast')}</p>
          <p className="text-3xl font-bold text-emerald-600">
            ${totalWeighted.toLocaleString(undefined, { minimumFractionDigits: 2 })}
          </p>
        </div>
      </div>

      <div className="flex gap-4 overflow-x-auto pb-4 flex-1">
        {STAGES.map(stage => (
          <div key={stage}
            className={`flex-shrink-0 w-80 bg-gray-50 rounded-xl flex flex-col ${isPending ? 'opacity-75' : ''}`}
            onDragOver={(e) => e.preventDefault()}
            onDrop={(e) => handleDrop(e.dataTransfer.getData('opportunityId'), stage)}
          >
            <div className="p-4 border-b border-gray-200 flex justify-between items-center">
              <h3 className="font-semibold text-gray-700">{stage}</h3>
              <span className="bg-gray-200 text-gray-600 px-2 py-1 rounded-full text-xs font-medium">
                {optimisticOpportunities.filter(o => o.stage === stage).length}
              </span>
            </div>
            <div className="p-4 flex-1 overflow-y-auto">
              {optimisticOpportunities.filter(o => o.stage === stage).map(opp => (
                <div key={opp.id} draggable onDragStart={(e) => handleDragStart(e, opp.id)}>
                  <OpportunityCard opportunity={opp} onOpenDetail={(o) => setDetailId(o.id)} />
                  {opp.status === 'Won' && (
                    <>
                      <CreateQuotationForm opportunity={opp} onCreated={reload} />
                      <CreateSalesOrderForm opportunity={opp} onCreated={reload} />
                    </>
                  )}
                </div>
              ))}
            </div>
          </div>
        ))}
      </div>
      {state.error && (
        <div className="fixed bottom-4 right-4 bg-red-100 text-red-700 p-4 rounded-lg shadow-lg border border-red-200">
          <p className="font-bold">{t('action.errorUpdatingStage')}</p>
          <p className="text-sm">{translateErrorCode(i18n, state.errorCode, state.error)}</p>
        </div>
      )}
      <LossReasonDialog
        open={pendingLoss !== null}
        reasons={lossReasons}
        loadingReasons={loadingReasons}
        reasonsError={reasonsError}
        onClose={() => setPendingLoss(null)}
        onConfirm={confirmLoss}
      />
      <OpportunityDetailModal
        opportunityId={detailId}
        onClose={() => setDetailId(null)}
        onSaved={reload}
      />
    </div>
  );
}
