import { useState } from 'react'
import { MasterDataList, type ColumnDef } from '../../../components/master-data/MasterDataList'
import { useBankAccounts, createBankAccount, updateBankAccount } from '../api/useBankAccounts'
import type { BankAccount } from '../api/useBankAccounts'
import { useTenantStore } from '../../../store/useTenantStore'
import { BankAccountFormModal } from '../components/BankAccountFormModal'
import { useTranslation } from 'react-i18next'
import { usePagination } from '../../../lib/pagination'

export function BankAccountsView() {
  const { t } = useTranslation('banking')
  const companyId = useTenantStore((state) => state.companyId)

  const { page, pageSize, setPage } = usePagination(10)
  const { items, status, error, reload, totalCount, pageNumber } = useBankAccounts(companyId, page, pageSize)

  const [isModalOpen, setIsModalOpen] = useState(false)
  const [selectedAccount, setSelectedAccount] = useState<BankAccount | null>(null)

  const columns: ColumnDef<BankAccount>[] = [
    { header: t('bankAccounts.columns.name', 'Account Name'), accessor: 'accountName' },
    { header: t('bankAccounts.columns.bank', 'Bank'), accessor: 'bankName' },
    { header: t('bankAccounts.columns.number', 'Number'), accessor: 'accountNumber' },
    {
      header: t('bankAccounts.columns.balance', 'Balance'),
      accessor: (row) => row.lastReconciledBalance?.toLocaleString() || '-',
    },
    {
      header: t('bankAccounts.columns.status', 'Status'),
      accessor: (row) => (
        <span
          className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-semibold ${
            row.isActive ? 'bg-green-100 text-green-800' : 'bg-slate-100 text-slate-800'
          }`}
        >
          {row.isActive ? t('bankAccounts.active', 'Active') : t('bankAccounts.inactive', 'Disabled')}
        </span>
      ),
    },
  ]

  const handleSave = async (accountData: Partial<BankAccount>) => {
    if (selectedAccount) {
      await updateBankAccount(selectedAccount.id, accountData)
    } else {
      await createBankAccount({ ...accountData, companyId })
    }
    reload()
  }

  if (status === 'loading' && !items.length) return <div className="p-4">Loading bank accounts...</div>
  if (error) return <div className="p-4 text-red-600">Error loading bank accounts: {error.message}</div>

  return (
    <div className="p-4">
      <MasterDataList
        title={t('bankAccounts.manageTitle', 'Bank Accounts')}
        subtitle={t('bankAccounts.manageSubtitle', 'Manage your bank profiles and their GL links.')}
        newButtonText={t('bankAccountForm.titleNew', 'New Bank Account')}
        searchPlaceholder={t('bankAccounts.search', 'Search bank accounts...')}
        data={items}
        columns={columns}
        onNew={() => {
          setSelectedAccount(null)
          setIsModalOpen(true)
        }}
        onRowClick={(row) => {
          setSelectedAccount(row)
          setIsModalOpen(true)
        }}
        pagination={
          totalCount > 0
            ? {
                totalCount: totalCount,
                page: pageNumber,
                pageSize: pageSize,
                onPageChange: setPage,
              }
            : undefined
        }
      />

      <BankAccountFormModal
        isOpen={isModalOpen}
        onClose={() => setIsModalOpen(false)}
        onSave={handleSave}
        initialData={selectedAccount}
        companyId={companyId}
      />
    </div>
  )
}
