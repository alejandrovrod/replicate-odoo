import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

/**
 * Mirrors `BankAccountDto` (GET / POST / PUT /api/v1/bank-accounts) as serialized by
 * System.Text.Json: camelCase properties, `rowVersion` as a base64 string.
 */
export interface BankAccount {
  id: string
  companyId: string
  accountName: string
  bankName: string
  accountNumber: string
  currencyId?: string | null
  glAccountId: string
  lastReconciledBalance: number
  lastReconciledDate?: string | null
  isActive: boolean
  /** Base64 rowversion token from GET; echoed back on PUT for optimistic concurrency. */
  rowVersion?: string
}

export function useBankAccounts(companyId: string, page = 1, pageSize = 50) {
  return useApiList<BankAccount>('/v1/BankAccounts', { companyId, page, pageSize }, Boolean(companyId))
}

/** POST body (mirrors `CreateBankAccountCommand`). */
export async function createBankAccount(payload: Partial<BankAccount>): Promise<BankAccount> {
  const response = await apiClient.post<BankAccount>('/v1/BankAccounts', {
    companyId: payload.companyId,
    accountName: payload.accountName,
    bankName: payload.bankName,
    accountNumber: payload.accountNumber,
    glAccountId: payload.glAccountId,
    currencyId: payload.currencyId ?? null,
    isActive: payload.isActive ?? true,
  })
  return response.data
}

/**
 * PUT body (mirrors `UpdateBankAccountCommand`): always carries the original
 * `rowVersion` so a concurrent change resolves to 409 instead of silently winning.
 */
export async function updateBankAccount(id: string, payload: Partial<BankAccount>): Promise<BankAccount> {
  const response = await apiClient.put<BankAccount>(`/v1/BankAccounts/${id}`, {
    id,
    companyId: payload.companyId,
    accountName: payload.accountName,
    bankName: payload.bankName,
    accountNumber: payload.accountNumber,
    glAccountId: payload.glAccountId,
    currencyId: payload.currencyId ?? null,
    isActive: payload.isActive ?? true,
    rowVersion: payload.rowVersion,
  })
  return response.data
}
