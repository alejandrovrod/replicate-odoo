import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
  CustomFieldsRenderer,
  parseCustomData,
  selectOptions,
  serializeCustomData,
  type CustomFieldDefinition,
} from './CustomFieldsRenderer'
import { i18n, loadTestFixtures } from '../../test/i18n-fixtures'

// Same no-op backend rationale as PeriodClosingList.test.tsx: components ask the singleton.
vi.mock('i18next-http-backend', () => ({
  default: class NoopBackend {
    static type = 'backend' as const
    init(): void {}
    read(
      _language: string,
      _namespace: string,
      callback: (err: null, data: Record<string, never>) => void,
    ): void {
      callback(null, {})
    }
  },
}))

const DEFINITIONS: CustomFieldDefinition[] = [
  {
    id: 'd1',
    entityName: 'Customer',
    fieldName: 'bloodType',
    fieldType: 'string',
    isRequired: true,
    isActive: true,
  },
  {
    id: 'd2',
    entityName: 'Customer',
    fieldName: 'vip',
    fieldType: 'boolean',
    isRequired: false,
    isActive: true,
  },
  {
    id: 'd3',
    entityName: 'Customer',
    fieldName: 'origin',
    fieldType: 'select',
    isRequired: false,
    isActive: true,
    validationJsonSchema: JSON.stringify({ options: ['local', 'imported'] }),
  },
]

describe('CustomFieldsRenderer (extensibility Fase 1, gap D4)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle(
      'en',
      'common',
      { customFields: { title: 'Custom Fields', selectNone: '-- Select --' } },
      true,
      true,
    )
    await i18n.changeLanguage('en')
  })

  it('renders nothing when the catalog is empty', () => {
    const { container } = render(
      <CustomFieldsRenderer definitions={[]} values={{}} onChange={() => {}} />,
    )
    expect(container).toBeEmptyDOMElement()
  })

  it('renders one input per definition and reports changes', async () => {
    const user = userEvent.setup()
    const seen: Array<[string, unknown]> = []
    render(
      <CustomFieldsRenderer
        definitions={DEFINITIONS}
        values={{ bloodType: 'O+' }}
        onChange={(field, value) => seen.push([field, value])}
      />,
    )

    expect(screen.getByDisplayValue('O+')).toBeInTheDocument()
    expect(screen.getByRole('checkbox', { name: 'vip' })).toBeInTheDocument()
    expect(screen.getByRole('combobox')).toBeInTheDocument()

    await user.click(screen.getByRole('checkbox', { name: 'vip' }))
    expect(seen).toEqual([['vip', true]])
  })

  it('marks required fields', () => {
    render(<CustomFieldsRenderer definitions={DEFINITIONS} values={{}} onChange={() => {}} />)
    expect(screen.getByText('bloodType *')).toBeInTheDocument()
  })
})

describe('custom data helpers', () => {
  it('parseCustomData tolerates malformed payloads', () => {
    expect(parseCustomData(null)).toEqual({})
    expect(parseCustomData('')).toEqual({})
    expect(parseCustomData('{nope')).toEqual({})
    expect(parseCustomData('{"a": 1}')).toEqual({ a: 1 })
  })

  it('serializeCustomData drops empty values and nulls the empty set', () => {
    expect(serializeCustomData({})).toBeNull()
    expect(serializeCustomData({ a: '', b: undefined, c: null })).toBeNull()
    expect(serializeCustomData({ a: 'x', b: '' })).toBe(JSON.stringify({ a: 'x' }))
  })

  it('selectOptions reads the options array', () => {
    expect(selectOptions(DEFINITIONS[2])).toEqual(['local', 'imported'])
    expect(selectOptions(DEFINITIONS[0])).toEqual([])
  })
})
