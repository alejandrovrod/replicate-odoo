import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { LossReasonDialog } from './LossReasonDialog'
import { loadTestFixtures } from '../../../test/i18n-fixtures'

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

const REASONS = [
  { id: 'r-price', code: 'PRICE', name: 'Lost on Price' },
  { id: 'r-comp', code: 'COMPETITOR', name: 'Lost to Competitor' },
]

describe('LossReasonDialog (CRM parity close dialog)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
  })

  it('keeps confirm disabled until a catalog reason is picked', async () => {
    const user = userEvent.setup()
    const onConfirm = vi.fn()
    render(
      <LossReasonDialog
        open
        reasons={REASONS}
        loadingReasons={false}
        reasonsError={null}
        onClose={() => {}}
        onConfirm={onConfirm}
      />,
    )

    const confirm = screen.getByRole('button', { name: 'Close as Lost' })
    expect(confirm).toBeDisabled()

    await user.selectOptions(screen.getByLabelText('Loss Reason'), 'r-comp')
    expect(confirm).toBeEnabled()

    await user.type(screen.getByLabelText('Observations'), 'Priced 15% lower')
    await user.click(confirm)

    expect(onConfirm).toHaveBeenCalledWith('r-comp', 'Priced 15% lower')
  })

  it('surfaces the catalog load failure instead of the picker', async () => {
    render(
      <LossReasonDialog
        open
        reasons={[]}
        loadingReasons={false}
        reasonsError="Network down"
        onClose={() => {}}
        onConfirm={() => {}}
      />,
    )

    expect(screen.getByText('Network down')).toBeInTheDocument()
    expect(screen.queryByLabelText('Loss Reason')).not.toBeInTheDocument()
  })
})
