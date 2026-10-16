import { apiClient } from '../../../api/client'
import { useApiList } from '../../../lib/useApiList'

/** Mirrors `Erp.Domain.Entities.SupplierHoldType` (serialized as the enum NAME). */
export type SupplierHoldType = 'None' | 'Invoices' | 'Payments' | 'All'

/**
 * One per-company account mapping (`SupplierCompanyAccountDto`, spec gap D2). Served inside
 * `Supplier.accounts`; `SupplierCompanyAccountInput` is the write shape (no `id`).
 */
export interface SupplierCompanyAccount {
  id: string
  companyId: string
  payableAccountId: string
  advanceAccountId?: string | null
}

export interface SupplierCompanyAccountInput {
  companyId: string
  payableAccountId: string
  advanceAccountId?: string | null
}

/**
 * Mirrors `SupplierDto` (GET / POST / PUT /api/v1/suppliers) as serialized by
 * System.Text.Json: camelCase properties, `rowVersion` as a base64 string.
 */
export interface Supplier {
  id: string
  code: string
  name: string
  taxId?: string
  defaultPayableAccountId?: string | null
  currencyId?: string | null
  paymentTermsDays?: number
  outstandingAmount?: number
  isActive: boolean
  createdAt?: string
  /** Base64 rowversion token from GET; echoed back on PUT for optimistic concurrency. */
  rowVersion?: string
  /** Supplier group classification (spec gap D1). */
  supplierGroupId?: string | null
  supplierGroupCode?: string | null
  supplierGroupName?: string | null
  /** Commercial terms (spec gap D3). */
  paymentTermsTemplateId?: string | null
  defaultPriceListId?: string | null
  /** Governance (spec gap D4). */
  isFrozen?: boolean
  onHold?: boolean
  holdType?: SupplierHoldType
  /** ISO date (yyyy-MM-dd) after which the hold auto-releases; null means never. */
  releaseDate?: string | null
  /** Per-company payable/advance accounts (spec gap D2). */
  accounts?: SupplierCompanyAccount[]
}

/** GET /api/v1/suppliers filters (spec §5). */
export interface SupplierFilters {
  groupId?: string
  onHold?: boolean
  companyId?: string
}

export function useSuppliers(
  page = 1,
  pageSize = 50,
  filters: SupplierFilters = {},
) {
  // Suppliers are unique per tenant, so we don't necessarily pass companyId unless it's for filtering
  return useApiList<Supplier>(
    '/v1/suppliers',
    {
      page,
      pageSize,
      groupId: filters.groupId,
      onHold: filters.onHold,
      companyId: filters.companyId,
    },
    true,
  )
}

const companyAccountBody = (accounts?: SupplierCompanyAccountInput[]) =>
  accounts?.map((account) => ({
    companyId: account.companyId,
    payableAccountId: account.payableAccountId,
    advanceAccountId: account.advanceAccountId ?? null,
  }))

/** POST body (mirrors `CreateSupplierCommand`). */
export async function createSupplier(payload: Partial<Supplier> & {
  companyAccounts?: SupplierCompanyAccountInput[]
}): Promise<Supplier> {
  const response = await apiClient.post<Supplier>('/v1/suppliers', {
    code: payload.code,
    name: payload.name,
    taxId: payload.taxId ?? '',
    defaultPayableAccountId: payload.defaultPayableAccountId ?? null,
    currencyId: payload.currencyId ?? null,
    paymentTermsDays: payload.paymentTermsDays ?? 30,
    isActive: payload.isActive ?? true,
    supplierGroupId: payload.supplierGroupId ?? null,
    paymentTermsTemplateId: payload.paymentTermsTemplateId ?? null,
    defaultPriceListId: payload.defaultPriceListId ?? null,
    onHold: payload.onHold ?? false,
    holdType: payload.holdType ?? 'None',
    releaseDate: payload.releaseDate ?? null,
    companyAccounts: companyAccountBody(payload.companyAccounts),
  })
  return response.data
}

/**
 * PUT body (mirrors `UpdateSupplierCommand`): always carries the original
 * `rowVersion` so a concurrent change resolves to 409 instead of silently winning.
 * Note the command names the legacy payable account `payableAccountId` (create uses
 * `defaultPayableAccountId`), so the mapper keeps that asymmetry.
 */
export async function updateSupplier(id: string, payload: Partial<Supplier> & {
  companyAccounts?: SupplierCompanyAccountInput[]
}): Promise<Supplier> {
  const response = await apiClient.put<Supplier>(`/v1/suppliers/${id}`, {
    id,
    code: payload.code,
    name: payload.name,
    taxId: payload.taxId ?? '',
    paymentTermsDays: payload.paymentTermsDays ?? 30,
    payableAccountId: payload.defaultPayableAccountId ?? null,
    isActive: payload.isActive ?? true,
    rowVersion: payload.rowVersion,
    supplierGroupId: payload.supplierGroupId ?? null,
    paymentTermsTemplateId: payload.paymentTermsTemplateId ?? null,
    defaultPriceListId: payload.defaultPriceListId ?? null,
    isFrozen: payload.isFrozen ?? false,
    onHold: payload.onHold ?? false,
    holdType: payload.holdType ?? 'None',
    releaseDate: payload.releaseDate ?? null,
    companyAccounts: companyAccountBody(payload.companyAccounts),
  })
  return response.data
}

/** Body of `POST /api/v1/suppliers/{id}/hold` (mirrors `SetSupplierHoldRequest`). */
export interface SupplierHoldInput {
  onHold: boolean
  holdType: SupplierHoldType
  releaseDate?: string | null
  rowVersion?: string
}

/**
 * Suspends (`onHold: true`) or releases a supplier hold. Releases clear the type and date
 * server-side, so the caller may send `holdType: 'None'` and `releaseDate: null`.
 */
export async function setSupplierHold(id: string, payload: SupplierHoldInput): Promise<Supplier> {
  const response = await apiClient.post<Supplier>(`/v1/suppliers/${id}/hold`, {
    onHold: payload.onHold,
    holdType: payload.holdType,
    releaseDate: payload.releaseDate ?? null,
    rowVersion: payload.rowVersion,
  })
  return response.data
}
