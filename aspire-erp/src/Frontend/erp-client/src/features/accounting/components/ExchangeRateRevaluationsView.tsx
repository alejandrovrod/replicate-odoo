import React, { useState } from 'react';
import { useExchangeRateRevaluations, submitRevaluation, cancelRevaluation } from '../api/useExchangeRateRevaluations';
import { ExchangeRateRevaluationModal } from './ExchangeRateRevaluationModal';
import { Button } from '../../../components/ui/Button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table';
import { useTenantStore } from '../../../store/useTenantStore';
import { useTranslation } from 'react-i18next';

export const ExchangeRateRevaluationsView: React.FC = () => {
    const { t } = useTranslation('accounting');
    const companyId = useTenantStore((s) => s.companyId);
    const { items: revaluations, status, reload } = useExchangeRateRevaluations(companyId);
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [error, setError] = useState('');

    if (!companyId) return <div className="p-4">{t('missingTenant')}</div>;
    if (status === 'loading') return <div className="p-4">Loading...</div>;

    const handleSubmit = async (id: string, rowVersion: string) => {
        try {
            await submitRevaluation(id, rowVersion);
            reload();
        } catch (e: any) {
            setError(e.message || 'Failed to submit');
        }
    };

    const handleCancel = async (id: string, rowVersion: string) => {
        try {
            await cancelRevaluation(id, rowVersion);
            reload();
        } catch (e: any) {
            setError(e.message || 'Failed to cancel');
        }
    };

    return (
        <div className="space-y-4">
            <div className="flex justify-between items-center">
                <h2 className="text-xl font-bold">{t('fxRevaluation.title')}</h2>
                <Button onClick={() => setIsModalOpen(true)}>{t('fxRevaluation.newBtn')}</Button>
            </div>
            
            {error && <div className="text-red-600 bg-red-50 p-2 rounded">{error}</div>}

            <div className="bg-white rounded-lg shadow">
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>{t('fxRevaluation.colVoucher')}</TableHead>
                            <TableHead>{t('fxRevaluation.colDate')}</TableHead>
                            <TableHead>{t('fxRevaluation.colStatus')}</TableHead>
                            <TableHead className="text-right">{t('fxRevaluation.colActions')}</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {revaluations.map((rev) => (
                            <TableRow key={rev.id}>
                                <TableCell className="font-medium">{rev.voucherNo}</TableCell>
                                <TableCell>{new Date(rev.postingDate).toLocaleDateString()}</TableCell>
                                <TableCell>
                                    <span className={`px-2 py-1 rounded-full text-xs font-semibold
                                        ${rev.documentStatus === 'Draft' ? 'bg-slate-100 text-slate-800' :
                                          rev.documentStatus === 'Submitted' ? 'bg-emerald-100 text-emerald-800' :
                                          'bg-rose-100 text-rose-800'}`}>
                                        {rev.documentStatus === 'Draft' ? t('fxRevaluation.statusDraft') :
                                         rev.documentStatus === 'Submitted' ? t('fxRevaluation.statusSubmitted') :
                                         t('fxRevaluation.statusCancelled')}
                                    </span>
                                </TableCell>
                                <TableCell className="text-right">
                                    <div className="flex justify-end gap-2">
                                        {rev.documentStatus === 'Draft' && (
                                            <Button size="sm" onClick={() => handleSubmit(rev.id, rev.rowVersion)}>{t('fxRevaluation.submitBtn')}</Button>
                                        )}
                                        {rev.documentStatus === 'Submitted' && (
                                            <Button size="sm" variant="destructive" onClick={() => handleCancel(rev.id, rev.rowVersion)}>{t('fxRevaluation.cancelBtn')}</Button>
                                        )}
                                    </div>
                                </TableCell>
                            </TableRow>
                        ))}
                        {revaluations.length === 0 && (
                            <TableRow>
                                <TableCell colSpan={4} className="text-center py-8 text-gray-500">{t('fxRevaluation.empty')}</TableCell>
                            </TableRow>
                        )}
                    </TableBody>
                </Table>
            </div>

            {isModalOpen && (
                <ExchangeRateRevaluationModal 
                    onClose={() => {
                        setIsModalOpen(false);
                        reload();
                    }} 
                />
            )}
        </div>
    );
};
