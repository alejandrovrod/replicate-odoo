import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { BatchSelector } from './BatchSelector'
import { apiClient } from '../../../api/client'
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

vi.mock('../../../api/client', () => ({
  ApiError: class MockApiError extends Error {
    status: number
    code?: string
    constructor(status: number, title: string, detail: string, code?: string) {
      super(detail || title)
      this.status = status
      this.code = code
    }
  },
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn() },
}))

const get = () => apiClient.get as unknown as Mock

describe('BatchSelector (batch-and-serial Fase 3, gap D5)', () => {
  beforeEach(async () => {
    await loadTestFixtures()
    i18n.addResourceBundle('en', 'stock', { tracking: {} }, true, true)
    await i18n.changeLanguage('en')
    get().mockReset()
  })

  it('lists FEFO batches with quantities and blocks expired rows', async () => {
    get().mockResolvedValue({
      data: [
        {
          batchId: 'b-soon',
          batchNo: 'LOT-SOON',
          expiryDate: '2026-12-31',
          isDisabled: false,
          qty: 10,
          isExpired: false,
        },
        {
          batchId: 'b-old',
          batchNo: 'LOT-OLD',
          expiryDate: '2026-01-01',
          isDisabled: false,
          qty: 5,
          isExpired: true,
        },
      ],
    })

    render(<BatchSelector itemId="item-1" warehouseId="wh-1" value="" onChange={() => {}} />)

    expect(await screen.findByText(/LOT-SOON/)).toBeInTheDocument()
    expect(get()).toHaveBeenCalledWith('/v1/batches/availability', {
      params: { itemId: 'item-1', warehouseId: 'wh-1' },
    })

    // Expired rows render but are not selectable.
    const expired = screen.getByText(/LOT-OLD/) as HTMLOptionElement
    expect(expired.disabled).toBe(true)
    expect(screen.getByText(/EXPIRED/)).toBeInTheDocument()
  })

  it('shows the empty state when no batches hold stock', async () => {
    get().mockResolvedValue({ data: [] })

    render(<BatchSelector itemId="item-1" warehouseId="wh-1" value="" onChange={() => {}} />)

    expect(await screen.findByText('No batches with stock')).toBeInTheDocument()
  })
})
