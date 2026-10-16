import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { SerialNoPickerModal, parseSerialInput } from './SerialNoPickerModal'
import { i18n, loadTestFixtures } from '../../../test/i18n-fixtures'

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

describe('parseSerialInput', () => {
  it('splits on newlines, commas, semicolons and whitespace', () => {
    expect(parseSerialInput('SN-1\nSN-2, SN-3;SN-4  SN-5')).toEqual({
      serials: ['SN-1', 'SN-2', 'SN-3', 'SN-4', 'SN-5'],
      duplicates: [],
    })
  })

  it('drops blanks and collapses duplicates', () => {
    expect(parseSerialInput('\nSN-1\nSN-1\n\nSN-2\n')).toEqual({
      serials: ['SN-1', 'SN-2'],
      duplicates: ['SN-1'],
    })
  })

  it('handles empty input', () => {
    expect(parseSerialInput('   ')).toEqual({ serials: [], duplicates: [] })
  })
})

describe('SerialNoPickerModal (batch-and-serial Fase 3, gap D5)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle('en', 'stock', { tracking: {} }, true, true)
    await i18n.changeLanguage('en')
  })

  it('applies only when the count matches and no duplicates exist', async () => {
    const user = userEvent.setup()
    const applied: string[][] = []
    render(
      <SerialNoPickerModal
        isOpen
        onClose={() => {}}
        itemLabel="LAP-001"
        expectedCount={2}
        initial={['SN-1001']}
        onApply={(serials) => applied.push(serials)}
      />,
    )

    // 1 of 2: the apply button stays disabled.
    expect(screen.getByText('1 of 2 serials')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Apply 1 serials' })).toBeDisabled()

    await user.clear(screen.getByRole('textbox', { name: 'Serial numbers' }))
    await user.type(screen.getByRole('textbox', { name: 'Serial numbers' }), 'SN-1001\nSN-1002')

    expect(screen.getByText('2 of 2 serials')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Apply 2 serials' }))
    expect(applied).toEqual([['SN-1001', 'SN-1002']])
  })

  it('flags pasted duplicates and blocks apply', async () => {
    const user = userEvent.setup()
    render(
      <SerialNoPickerModal
        isOpen
        onClose={() => {}}
        itemLabel="LAP-001"
        expectedCount={2}
        initial={[]}
        onApply={() => {}}
      />,
    )

    await user.type(screen.getByRole('textbox', { name: 'Serial numbers' }), 'SN-1\nSN-1')
    expect(screen.getByText('duplicated: SN-1')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Apply 1 serials' })).toBeDisabled()
  })
})
