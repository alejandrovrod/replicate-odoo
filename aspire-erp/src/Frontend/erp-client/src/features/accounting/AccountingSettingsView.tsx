import React, { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Settings, Building } from 'lucide-react'
import { Button } from '../../components/ui/Button'
import { CompanySettingsModal } from './components/CompanySettingsModal'

export function AccountingSettingsView() {
  const { t } = useTranslation('accounting')
  const [isModalOpen, setIsModalOpen] = useState(false)

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-slate-900">
            {t('page.accountingSettings.title', 'Accounting Settings')}
          </h2>
          <p className="text-sm text-slate-500">
            {t('page.accountingSettings.subtitle', 'Manage company-wide accounting configuration')}
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        <div className="rounded-xl border border-slate-200 bg-white shadow-sm overflow-hidden flex flex-col">
          <div className="p-6 flex-1 space-y-4">
            <div className="h-12 w-12 rounded-lg bg-sky-50 flex items-center justify-center text-sky-600">
              <Building className="h-6 w-6" />
            </div>
            <div>
              <h3 className="text-lg font-semibold text-slate-900">
                {t('companySettings.title', 'Company Accounting Settings')}
              </h3>
              <p className="mt-1 text-sm text-slate-500">
                {t('companySettings.description', 'Configure frozen accounts date and default retained earnings account for the company.')}
              </p>
            </div>
          </div>
          <div className="bg-slate-50 px-6 py-4 border-t border-slate-100">
            <Button onClick={() => setIsModalOpen(true)} className="w-full justify-center">
              <Settings className="h-4 w-4 mr-2" />
              {t('action.configure', 'Configure')}
            </Button>
          </div>
        </div>
      </div>

      {isModalOpen && (
        <CompanySettingsModal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} />
      )}
    </div>
  )
}
