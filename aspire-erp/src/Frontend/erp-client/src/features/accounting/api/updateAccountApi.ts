import { apiClient } from '../../../api/client'
import type { AccountFormData } from '../components/AccountFormModal'

/** POST /v1/accounts — full create payload. */
export async function createAccountApi(data: AccountFormData): Promise<void> {
  await apiClient.post('/v1/accounts', data)
}

export interface UpdateAccountPayload {
  id: string
  companyId: string
  accountName: string
  isActive: boolean
  /** Base64 rowversion token echoed back for optimistic concurrency (409 on stale). */
  rowVersion?: string
  /**
   * Tri-state reparent mirror of `UpdateAccountCommand` (COA-02/COA-03):
   * omitted = untouched; `changeParent: true + parentAccountId: null` = move to root.
   */
  changeParent?: boolean
  parentAccountId?: string | null
  /**
   * Group↔leaf conversion mirror (COA-04): null/omitted = no change,
   * otherwise guarded server-side by `EnsureValidIsGroupChange`.
   */
  isGroup?: boolean | null
}

/**
 * PUT /v1/accounts/{id} — mirrors UpdateAccountCommand: name + active always travel,
 * plus the original rowVersion token. Optional tri-state members (changeParent /
 * parentAccountId / isGroup) travel only when explicitly set. ApiError (incl. 409
 * ProblemDetails) propagates to the caller so container + modal can each render
 * domain feedback.
 */
export async function updateAccountApi(payload: UpdateAccountPayload): Promise<void> {
  await apiClient.put(`/v1/accounts/${payload.id}`, payload)
}

/**
 * PUT /v1/accounts/{id}/enable|disable — quick IsActive flip for the tree's
 * contextual Desactivar/Activar action (AccountsController.SetActiveAsync).
 * No rowVersion gate here by design (dedicated endpoint, not the editing PUT);
 * callers reload the tree afterwards so the fresh token arrives with the GET.
 */
export async function setAccountActiveApi(id: string, isActive: boolean): Promise<void> {
  await apiClient.put(`/v1/accounts/${id}/${isActive ? 'enable' : 'disable'}`)
}

/** Dispatches create vs update based on presence of `id`. */
export async function saveAccountApi(data: AccountFormData): Promise<void> {
  if (data.id) {
    const payload: UpdateAccountPayload = {
      id: data.id,
      companyId: data.companyId,
      accountName: data.accountName,
      isActive: data.isActive,
      // Bidirectional rowVersion flow: tree node → modal initialData → PUT echo →
      // caller reload() fetches the tree again carrying the NEW token (COA-G6).
      rowVersion: data.rowVersion,
    }
    // Forward conversion/reparent members only when the form explicitly carries
    // them; omitting keeps the backend's backward-compatible defaults (no-op).
    if (data.isGroup !== undefined) payload.isGroup = data.isGroup
    if (data.parentAccountId !== undefined || data.changeParent === true) {
      payload.changeParent = data.changeParent ?? data.parentAccountId !== undefined
      payload.parentAccountId = data.parentAccountId ?? null
    }
    await updateAccountApi(payload)
  } else {
    await createAccountApi(data)
  }
}
