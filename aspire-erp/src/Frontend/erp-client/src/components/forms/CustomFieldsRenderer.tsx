import { useTranslation } from 'react-i18next'
import { Input } from '../ui/Input'

/**
 * One row of GET /api/v1/customfielddefinitions (mirrors `CustomFieldDefinitionDto` as
 * serialized by System.Text.Json: camelCase properties).
 */
export interface CustomFieldDefinition {
  id: string
  entityName: string
  fieldName: string
  /** Vocabulary: string | number | boolean | date | select. */
  fieldType: string
  isRequired: boolean
  defaultValue?: string | null
  /** Extra constraints as a JSON object (options/pattern/min/max). */
  validationJsonSchema?: string | null
  isActive: boolean
}

/** Custom attribute values keyed by field name (the parsed form of `CustomData`). */
export type CustomFieldValues = Record<string, unknown>

/**
 * Parses a `CustomData` JSON string into values. Malformed or non-object payloads yield an
 * empty record (the form stays usable; the server remains the validator of record).
 */
export function parseCustomData(customData?: string | null): CustomFieldValues {
  if (!customData || customData.trim() === '') return {}
  try {
    const parsed: unknown = JSON.parse(customData)
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      return parsed as CustomFieldValues
    }
    return {}
  } catch {
    return {}
  }
}

/**
 * Serializes values back to the `CustomData` wire format. Returns null when no value is
 * set, so updates can distinguish "not sent" (null keeps the stored value server-side
 * only when the caller omits the key - here null simply means an empty payload).
 */
export function serializeCustomData(values: CustomFieldValues): string | null {
  const entries = Object.entries(values).filter(
    ([, value]) => value !== undefined && value !== null && value !== '',
  )
  if (entries.length === 0) return null
  return JSON.stringify(Object.fromEntries(entries))
}

/** Options declared in a select definition's `validationJsonSchema` (`{"options": [...]}`). */
export function selectOptions(definition: CustomFieldDefinition): string[] {
  if (!definition.validationJsonSchema) return []
  try {
    const parsed: unknown = JSON.parse(definition.validationJsonSchema)
    if (parsed && typeof parsed === 'object' && !Array.isArray(parsed)) {
      const options = (parsed as Record<string, unknown>).options
      if (Array.isArray(options)) return options.filter((o): o is string => typeof o === 'string')
    }
    return []
  } catch {
    return []
  }
}

interface CustomFieldsRendererProps {
  definitions: CustomFieldDefinition[]
  values: CustomFieldValues
  onChange: (fieldName: string, value: unknown) => void
  disabled?: boolean
}

const selectClassName =
  'flex h-10 w-full rounded-md border border-slate-200 bg-white px-3 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:border-slate-400 focus-visible:ring-2 focus-visible:ring-slate-200 disabled:opacity-50'

/**
 * Dynamic custom-attribute section (extensibility-engine Fase 1, gap D4): renders one input
 * per active `CustomFieldDefinition` of the entity. Supported types: string, number,
 * boolean, select (options from the definition) and date. An empty catalog renders nothing
 * (the host form decides whether to show the section title).
 *
 * Client-side input shaping only - the backend catalog validation stays authoritative, so
 * the form surfaces server rejections through its existing error path.
 */
export function CustomFieldsRenderer({
  definitions,
  values,
  onChange,
  disabled,
}: CustomFieldsRendererProps) {
  const { t } = useTranslation('common')

  if (definitions.length === 0) return null

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
      {definitions.map((definition) => {
        const value = values[definition.fieldName]
        const label = (
          <label className="text-sm font-medium">
            {definition.fieldName}
            {definition.isRequired ? ' *' : ''}
          </label>
        )
        const key = definition.id || definition.fieldName

        if (definition.fieldType === 'boolean') {
          return (
            <div key={key} className="flex items-center space-x-2 pt-6">
              <input
                type="checkbox"
                checked={value === true}
                onChange={(e) => onChange(definition.fieldName, e.target.checked)}
                disabled={disabled}
                aria-label={definition.fieldName}
                className="h-4 w-4 rounded border-slate-300 text-slate-900 focus:ring-slate-950"
              />
              <span className="text-sm font-medium">
                {definition.fieldName}
                {definition.isRequired ? ' *' : ''}
              </span>
            </div>
          )
        }

        if (definition.fieldType === 'select') {
          const options = selectOptions(definition)
          return (
            <div key={key} className="space-y-2">
              {label}
              <select
                value={typeof value === 'string' ? value : ''}
                onChange={(e) => onChange(definition.fieldName, e.target.value)}
                disabled={disabled}
                required={definition.isRequired}
                className={selectClassName}
              >
                <option value="">{t('customFields.selectNone', '-- Select --')}</option>
                {options.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
          )
        }

        if (definition.fieldType === 'number') {
          return (
            <div key={key} className="space-y-2">
              {label}
              <Input
                type="number"
                step="any"
                value={typeof value === 'number' ? value : ''}
                onChange={(e) =>
                  onChange(
                    definition.fieldName,
                    e.target.value === '' ? undefined : Number(e.target.value),
                  )
                }
                disabled={disabled}
                required={definition.isRequired}
              />
            </div>
          )
        }

        if (definition.fieldType === 'date') {
          return (
            <div key={key} className="space-y-2">
              {label}
              <Input
                type="date"
                value={typeof value === 'string' ? value : ''}
                onChange={(e) => onChange(definition.fieldName, e.target.value || undefined)}
                disabled={disabled}
                required={definition.isRequired}
              />
            </div>
          )
        }

        return (
          <div key={key} className="space-y-2">
            {label}
            <Input
              type="text"
              value={typeof value === 'string' ? value : ''}
              onChange={(e) => onChange(definition.fieldName, e.target.value || undefined)}
              disabled={disabled}
              required={definition.isRequired}
              placeholder={definition.defaultValue ?? ''}
            />
          </div>
        )
      })}
    </div>
  )
}
