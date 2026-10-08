import React, { useState } from 'react';
import { useExchangeRates } from '../api/useExchangeRates';
import { ExchangeRateFormModal } from './ExchangeRateFormModal';
import { Button } from '../../../components/ui/Button';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../../../components/ui/Table';
import { useTenantStore } from '../../../store/useTenantStore';
import { useTranslation } from 'react-i18next';

export const ExchangeRateList: React.FC = () => {
    const { t } = useTranslation('accounting');
    const companyId = useTenantStore((s) => s.companyId);
    const { items: rates, status, reload } = useExchangeRates(companyId);
    const [isModalOpen, setIsModalOpen] = useState(false);

    if (!companyId) return <div className="p-4">{t('missingTenant')}</div>;
    if (status === 'loading') return <div className="p-4">{t('exchangeRate.loading')}</div>;

    return (
        <div className="space-y-4">
            <div className="flex justify-between items-center">
                <h2 className="text-xl font-bold">{t('exchangeRate.title')}</h2>
                <Button onClick={() => setIsModalOpen(true)}>{t('exchangeRate.addBtn')}</Button>
            </div>
            
            <div className="bg-white rounded-lg shadow">
                <Table>
                    <TableHeader>
                        <TableRow>
                            <TableHead>{t('exchangeRate.colDate')}</TableHead>
                            <TableHead>{t('exchangeRate.colFrom')}</TableHead>
                            <TableHead>{t('exchangeRate.colTo')}</TableHead>
                            <TableHead className="text-right">{t('exchangeRate.colRate')}</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {rates.map((rate) => (
                            <TableRow key={rate.id}>
                                <TableCell>{new Date(rate.rateDate).toLocaleDateString()}</TableCell>
                                <TableCell>{rate.fromCurrencyId}</TableCell>
                                <TableCell>{rate.toCurrencyId}</TableCell>
                                <TableCell className="text-right">{rate.rate.toFixed(6)}</TableCell>
                            </TableRow>
                        ))}
                        {rates.length === 0 && (
                            <TableRow>
                                <TableCell colSpan={4} className="text-center py-8 text-gray-500">{t('exchangeRate.empty')}</TableCell>
                            </TableRow>
                        )}
                    </TableBody>
                </Table>
            </div>

            {isModalOpen && (
                <ExchangeRateFormModal 
                    onClose={() => {
                        setIsModalOpen(false);
                        reload();
                    }} 
                />
            )}
        </div>
    );
};
