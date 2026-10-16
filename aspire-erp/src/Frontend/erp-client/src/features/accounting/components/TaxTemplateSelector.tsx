import { useTranslation } from 'react-i18next'
import { useTaxTemplates, type TaxTemplate } from '../api/useTaxTemplates'

interface TaxTemplateSelectorProps {
  companyId: string
  category: string
  value: string
  onSelect: (template: TaxTemplate | null) => void
  disabled?: boolean
}

/**
 * Template picker (tax-engine Fase 2, gap D5): loads the company+category catalog and
 * hands the full template (lines included) to the host form, which expands it into
 * editable rows. Selecting the empty option clears back to manual rows.
 */
export function TaxTemplateSelector({
  companyId,
  category,
  value,
  onSelect,
  disabled,
}: TaxTemplateSelectorProps) {
  const { t } = useTranslation('accounting')
  const { templates, status } = useTaxTemplates(companyId, category)

  return (
    <div className="space-y-2">
      <label className="text-sm font-medium">{t('tax.templateLabel', 'Tax template')}</label>
      <select
        value={value}
        disabled={disabled || status === 'loading'}
        onChange={(e) => {
          const template = templates.find((tpl) => tpl.id === e.target.value) ?? null
          onSelect(template)
        }}
        className="flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50"
      >
        <option value="">
          {status === 'loading'
            ? t('tax.templateLoading', 'Loading templates…')
            : t('tax.templateNone', '-- Manual rows --')}
        </option>
        {templates.map((template) => (
          <option key={template.id} value={template.id}>
            {template.title}
            {template.isDefault ? ` (${t('tax.templateDefault', 'default')})` : ''} —{' '}
            {t('tax.templateLines', '{{count}} lines', { count: template.lines.length })}
          </option>
        ))}
      </select>
    </div>
  )
}
