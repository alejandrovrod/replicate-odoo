import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

export { getBanksApi } from './bankingApi'
export type { BankDto } from '../types'

/**
 * Mirrors `BankAccountDto` (GET / POST / PUT /api/v1/bank-accounts) as serialized by
 * System.Text.Json: camelCase properties, `rowVersion` as a base64 string.
 */
export interface BankAccount {
  id: string
  companyId: string
  accountName: string
  bankName: string
  /** Optional catalog reference (bank-catalog BNK-G3): null = free-text `bankName`. */
  bankId?: string | null
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

/** POST body (mirrors `CreateBankAccountCommand`; `bankId` optional per BNK-G3). */
export async function createBankAccount(payload: Partial<BankAccount>): Promise<BankAccount> {
  const response = await apiClient.post<BankAccount>('/v1/BankAccounts', {
    companyId: payload.companyId,
    accountName: payload.accountName,
    bankName: payload.bankName,
    // Backward-compatible: the key travels only once a catalog selection exists, so
    // pre-catalog backends (unknown-member tolerant STJ) and rows without bank keep
    // working; explicit null clears a previous selection on update.
    ...(payload.bankId !== undefined ? { bankId: payload.bankId } : {}),
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
    ...(payload.bankId !== undefined ? { bankId: payload.bankId } : {}),
    accountNumber: payload.accountNumber,
    glAccountId: payload.glAccountId,
    currencyId: payload.currencyId ?? null,
    isActive: payload.isActive ?? true,
    rowVersion: payload.rowVersion,
  })
  return response.data
}
