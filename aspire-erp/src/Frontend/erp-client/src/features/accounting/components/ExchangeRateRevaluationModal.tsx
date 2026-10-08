import React, { useState } from 'react';
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '../../../components/ui/Dialog';
import { Button } from '../../../components/ui/Button';
import { Input } from '../../../components/ui/Input';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table';
import { createRevaluation, getRevaluationPreview, type RevaluationPreview } from '../api/useExchangeRateRevaluations';
import { useTenantStore } from '../../../store/useTenantStore';
import { useTranslation } from 'react-i18next';

interface Props {
    onClose: () => void;
}

export const ExchangeRateRevaluationModal: React.FC<Props> = ({ onClose }) => {
    const { t } = useTranslation('accounting');
    const companyId = useTenantStore((s) => s.companyId);
    const [postingDate, setPostingDate] = useState(new Date().toISOString().split('T')[0]);
    const [allowance, setAllowance] = useState('0.01');
    const [fxAccountId, setFxAccountId] = useState('');

    const [preview, setPreview] = useState<RevaluationPreview | null>(null);
    const [isPreviewing, setIsPreviewing] = useState(false);
    const [isSaving, setIsSaving] = useState(false);
    const [error, setError] = useState('');

    const handlePreview = async () => {
        if (!companyId || !postingDate) return;
        setIsPreviewing(true);
        setError('');
        setPreview(null);
        try {
            const data = await getRevaluationPreview(companyId, new Date(postingDate).toISOString(), parseFloat(allowance) || 0, fxAccountId || undefined);
            setPreview(data);
        } catch (e: any) {
            setError(e.message || 'Preview error');
        } finally {
            setIsPreviewing(false);
        }
    };

    const handleCreate = async () => {
        if (!companyId) return;
        setIsSaving(true);
        setError('');
        try {
            await createRevaluation({
                companyId,
                postingDate: new Date(postingDate).toISOString(),
                roundingLossAllowance: parseFloat(allowance),
                exchangeGainLossAccountId: fxAccountId || undefined
            });
            onClose();
        } catch (e: any) {
            setError(e.message || 'Failed to create revaluation');
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <Dialog open={true} onOpenChange={onClose}>
            <DialogContent className="max-w-4xl max-h-[90vh] flex flex-col">
                <DialogHeader>
                    <DialogTitle>{t('fxRevaluation.formTitle')}</DialogTitle>
                </DialogHeader>
                
                {error && <div className="text-red-600 bg-red-50 p-2 rounded text-sm">{error}</div>}

                <div className="grid grid-cols-3 gap-4 mb-4">
                    <div>
                        <label className="block text-sm font-medium mb-1">{t('fxRevaluation.formDate')}</label>
                        <Input type="date" value={postingDate} onChange={e => setPostingDate(e.target.value)} />
                    </div>
                    <div>
                        <label className="block text-sm font-medium mb-1">{t('fxRevaluation.formAllowance')}</label>
                        <Input type="number" step="0.01" value={allowance} onChange={e => setAllowance(e.target.value)} />
                    </div>
                    <div>
                        <label className="block text-sm font-medium mb-1">{t('fxRevaluation.formAccount')}</label>
                        <Input type="text" value={fxAccountId} onChange={e => setFxAccountId(e.target.value)} placeholder={t('fxRevaluation.formAccountPlaceholder')} />
                    </div>
                </div>
                
                <div className="mb-4">
                    <Button type="button" variant="secondary" onClick={handlePreview} disabled={isPreviewing}>
                        {isPreviewing ? t('fxRevaluation.previewLoading') : t('fxRevaluation.previewBtn')}
                    </Button>
                </div>

                <div className="flex-1 overflow-auto rounded-lg bg-white">
                    <Table>
                        <TableHeader>
                            <TableRow>
                                <TableHead>{t('fxRevaluation.colAccount')}</TableHead>
                                <TableHead className="text-right">{t('fxRevaluation.colForeignBal')}</TableHead>
                                <TableHead className="text-right">{t('fxRevaluation.colBaseBal')}</TableHead>
                                <TableHead className="text-right">{t('fxRevaluation.colNewRate')}</TableHead>
                                <TableHead className="text-right">{t('fxRevaluation.colGainLoss')}</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {preview?.lines.map((l, i) => (
                                <TableRow key={i}>
                                    <TableCell>{l.accountId}</TableCell>
                                    <TableCell className="text-right">{l.balanceInAccountCurrency?.toFixed(2) ?? '0.00'}</TableCell>
                                    <TableCell className="text-right">{l.balanceInCompanyCurrency?.toFixed(2) ?? '0.00'}</TableCell>
                                    <TableCell className="text-right">{l.newExchangeRate?.toFixed(6) ?? '0.000000'}</TableCell>
                                    <TableCell className={`text-right font-medium ${(l.unrealizedGainLoss ?? 0) > 0 ? 'text-emerald-600' : 'text-rose-600'}`}>
                                        {l.unrealizedGainLoss?.toFixed(2) ?? '0.00'}
                                    </TableCell>
                                </TableRow>
                            ))}
                            {preview?.lines.length === 0 && (
                                <TableRow>
                                    <TableCell colSpan={5} className="text-center py-4 text-slate-500">{t('fxRevaluation.previewEmpty')}</TableCell>
                                </TableRow>
                            )}
                            {!preview && !isPreviewing && (
                                <TableRow>
                                    <TableCell colSpan={5} className="text-center py-4 text-slate-500">{t('fxRevaluation.previewPrompt')}</TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </div>
                
                {preview && (
                    <div className="mt-2 font-bold text-right text-lg">
                        {t('fxRevaluation.totalGainLoss')} <span className={(preview.totalGainLoss ?? 0) > 0 ? 'text-emerald-600' : 'text-rose-600'}>{preview.totalGainLoss?.toFixed(2) ?? '0.00'}</span>
                    </div>
                )}

                <DialogFooter className="mt-4">
                    <Button type="button" variant="outline" onClick={onClose}>{t('action.cancel', { ns: 'common' })}</Button>
                    <Button onClick={handleCreate} disabled={!preview || isSaving}>{t('fxRevaluation.saveDraftBtn')}</Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
};
