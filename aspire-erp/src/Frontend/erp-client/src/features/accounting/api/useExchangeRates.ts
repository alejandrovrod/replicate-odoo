import { useCallback, useEffect, useState } from 'react';
import { ApiError, apiClient } from '../../../api/client';

export interface ExchangeRate {
    id: string;
    fromCurrencyId: string;
    toCurrencyId: string;
    rateDate: string;
    rate: number;
    rowVersion: string;
}

export type ExchangeRateStatus = 'idle' | 'loading' | 'success' | 'error';

export function useExchangeRates(companyId?: string) {
    const [items, setItems] = useState<ExchangeRate[]>([]);
    const [status, setStatus] = useState<ExchangeRateStatus>('idle');
    const [error, setError] = useState<ApiError | null>(null);

    const fetchRates = useCallback(async () => {
        if (!companyId) return;
        setStatus('loading');
        try {
            const response = await apiClient.get<ExchangeRate[]>('/v1/exchange-rates', {
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
        fetchRates();
    }, [fetchRates]);

    return { items, status, error, reload: fetchRates };
}

export async function upsertExchangeRate(payload: { fromCurrencyId: string; toCurrencyId: string; rateDate: string; rate: number }): Promise<ExchangeRate> {
    const idempotencyKey = crypto.randomUUID();
    const response = await apiClient.post<ExchangeRate>('/v1/exchange-rates', payload, {
        headers: { 'Idempotency-Key': idempotencyKey }
    });
    return response.data!;
}
