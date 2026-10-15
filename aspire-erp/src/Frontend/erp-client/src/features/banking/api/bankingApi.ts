import { apiClient } from '../../../api/client'
import { MAX_PAGE_SIZE, type PagedResult } from '../../../lib/pagination'
import type { BankDto } from '../types'

export type { BankDto } from '../types'

const BANKS_PATH = '/v1/Banks'

function unwrapBanks(payload: unknown): BankDto[] {
  if (Array.isArray(payload)) return payload as BankDto[]
  if (payload && typeof payload === 'object' && Array.isArray((payload as PagedResult<BankDto>).items)) {
    return (payload as PagedResult<BankDto>).items
  }
  return []
}

/**
 * Bank catalog (BNK-G1/G2/G5): GET /api/v1/banks filtered by country.
 *
 * `countryCode` source: the caller's company country (`Company.CountryCode` invariant,
 * spec bank-catalog §3 D3). It defaults to "MX" for read-only filtering — never persisted —
 * because the company row may predate the `CountryCode` column (nullable, no DB default).
 *
 * Tolerant by design (backend Fase 1 ships in parallel): accepts either the certified
 * `countryCode` spelling or `country` (both are sent; unknown query keys are ignored),
 * unwraps either a bare array or a `PagedResult` envelope, and resolves to `[]` on any
 * unrecognized shape so the combobox falls back to free-text entry (BNK-G3).
 */
export async function getBanksApi(countryCode?: string, search?: string): Promise<BankDto[]> {
  const country = (countryCode ?? 'MX').toUpperCase()
  const params: Record<string, string | number> = { page: 1, pageSize: MAX_PAGE_SIZE }
  params.country = country
  params.countryCode = country
  if (search) params.search = search
  const response = await apiClient.get<BankDto[] | PagedResult<BankDto>>(BANKS_PATH, { params })
  return unwrapBanks(response.data)
}
