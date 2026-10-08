import { useCallback, useEffect, useState } from 'react';
import { ApiError, apiClient } from '../../../api/client';

export interface RevaluationLine {
    accountId: string;
    accountCode: string;
    balanceInAccountCurrency: number;
    balanceInCompanyCurrency: number;
    currentExchangeRate: number;
    newExchangeRate: number;
    newBalanceInCompanyCurrency: number;
    unrealizedGainLoss: number;
    zeroBalance: boolean;
}

export interface Revaluation {
    id: string;
    companyId: string;
    voucherNo: string;
    postingDate: string;
    exchangeGainLossAccountId?: string;
    roundingLossAllowance: number;
    documentStatus: string;
    remarks?: string;
    rowVersion: string;
}

export interface RevaluationPreview {
    totalGainLoss: number;
    lines: RevaluationLine[];
}

export type RevaluationStatus = 'idle' | 'loading' | 'success' | 'error';

export function useExchangeRateRevaluations(companyId?: string) {
    const [items, setItems] = useState<Revaluation[]>([]);
    const [status, setStatus] = useState<RevaluationStatus>('idle');
    const [error, setError] = useState<ApiError | null>(null);

    const fetchRevs = useCallback(async () => {
        if (!companyId) return;
        setStatus('loading');
        try {
            const response = await apiClient.get<Revaluation[]>('/v1/exchange-rate-revaluations', {
                params: { companyId }
            });
            setItems(response.data ?? []);
            setStatus('success');
        } catch (cause: unknown) {
            setError(cause instanceof ApiError ? cause : new ApiError(0, 'Unexpected Error', String(cause)));
            setStatus('error');
        }
    }, [companyId]);

    useEffect(() => {
        fetchRevs();
    }, [fetchRevs]);

    return { items, status, error, reload: fetchRevs };
}

export async function getRevaluationPreview(companyId: string, postingDate: string, allowance: number, fxAccountId?: string): Promise<RevaluationPreview> {
    const response = await apiClient.get<RevaluationPreview>('/v1/exchange-rate-revaluations/preview', {
        params: { companyId, postingDate, allowance, fxAccountId }
    });
    return response.data!;
}

export async function createRevaluation(payload: { companyId: string; postingDate: string; roundingLossAllowance: number; exchangeGainLossAccountId?: string; remarks?: string }): Promise<Revaluation> {
    const response = await apiClient.post<Revaluation>('/v1/exchange-rate-revaluations', payload, {
        headers: { 'Idempotency-Key': crypto.randomUUID() }
    });
    return response.data!;
}

export async function submitRevaluation(id: string, rowVersion: string): Promise<void> {
    await apiClient.post(`/v1/exchange-rate-revaluations/${id}/submit`, { rowVersion }, {
        headers: { 'Idempotency-Key': crypto.randomUUID() }
    });
}

export async function cancelRevaluation(id: string, rowVersion: string): Promise<void> {
    await apiClient.post(`/v1/exchange-rate-revaluations/${id}/cancel`, { rowVersion }, {
        headers: { 'Idempotency-Key': crypto.randomUUID() }
    });
}
