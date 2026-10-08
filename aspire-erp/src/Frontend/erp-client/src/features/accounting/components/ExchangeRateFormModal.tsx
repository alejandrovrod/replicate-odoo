import React, { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../../components/ui/Dialog';
import { Button } from '../../../components/ui/Button';
import { Input } from '../../../components/ui/Input';
import { upsertExchangeRate } from '../api/useExchangeRates';
import { useCurrencies } from '../api/useCurrencies';
import { ApiError } from '../../../api/client';
import { useTranslation } from 'react-i18next';

interface Props {
    onClose: () => void;
}

export const ExchangeRateFormModal: React.FC<Props> = ({ onClose }) => {
    const { t } = useTranslation('accounting');
    const { items: currencies } = useCurrencies();
    const [isPending, setIsPending] = useState(false);
    const [error, setError] = useState('');

    const [fromCurrency, setFromCurrency] = useState('');
    const [toCurrency, setToCurrency] = useState('');
    const [rateDate, setRateDate] = useState(new Date().toISOString().split('T')[0]);
    const [rate, setRate] = useState('');

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setIsPending(true);
        setError('');
        try {
            await upsertExchangeRate({
                fromCurrencyId: fromCurrency,
                toCurrencyId: toCurrency,
                rateDate: new Date(rateDate).toISOString(),
                rate: parseFloat(rate)
            });
            onClose();
        } catch (err: unknown) {
            setError(err instanceof ApiError ? err.message : String(err));
        } finally {
            setIsPending(false);
        }
    };

    return (
        <Dialog open={true} onOpenChange={onClose}>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>{t('exchangeRate.formTitle')}</DialogTitle>
                </DialogHeader>
                {error && <div className="text-red-600 text-sm mb-4">{error}</div>}
                <form onSubmit={handleSubmit} className="space-y-4">
                    <div>
                        <label className="block text-sm font-medium mb-1">{t('exchangeRate.colFrom')}</label>
                        <select className="flex h-9 w-full rounded-md border border-slate-200 bg-transparent px-3 py-1 text-sm shadow-sm transition-colors" value={fromCurrency} onChange={e => setFromCurrency(e.target.value)} required>
                            <option value="">{t('exchangeRate.formSelect')}</option>
                            {currencies.map(c => <option key={c.id} value={c.id}>{c.code}</option>)}
                        </select>
                    </div>
                    <div>
                        <label className="block text-sm font-medium mb-1">{t('exchangeRate.colTo')}</label>
                        <select className="flex h-9 w-full rounded-md border border-slate-200 bg-transparent px-3 py-1 text-sm shadow-sm transition-colors" value={toCurrency} onChange={e => setToCurrency(e.target.value)} required>
                            <option value="">{t('exchangeRate.formSelect')}</option>
                            {currencies.map(c => <option key={c.id} value={c.id}>{c.code}</option>)}
                        </select>
                    </div>
                    <div>
                        <label className="block text-sm font-medium mb-1">{t('exchangeRate.colDate')}</label>
                        <Input type="date" value={rateDate} onChange={e => setRateDate(e.target.value)} required />
                    </div>
                    <div>
                        <label className="block text-sm font-medium mb-1">{t('exchangeRate.colRate')}</label>
                        <Input type="number" step="0.000001" value={rate} onChange={e => setRate(e.target.value)} required />
                    </div>
                    <DialogFooter>
                        <Button type="button" variant="outline" onClick={onClose}>{t('action.cancel', { ns: 'common' })}</Button>
                        <Button type="submit" disabled={isPending}>{isPending ? t('state.saving', { ns: 'common' }) : t('action.save', { ns: 'common' })}</Button>
                    </DialogFooter>
                </form>
            </DialogContent>
        </Dialog>
    );
};
