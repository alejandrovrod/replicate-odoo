import axios from 'axios'
import { publicClient } from '../../api/publicClient'

export interface TenantLookup {
  id: string
  name: string
  subdomain: string
  defaultLanguage: string
}

export interface TenantRegistrationResult {
  tenantId: string
  companyId: string
  adminUserId: string
  tenantName: string
  subdomain: string
  adminEmail: string
  defaultLanguage: string
}

export interface RegisterTenantPayload {
  tenantName: string
  companyName: string
  subdomain: string
  adminFullName: string
  adminEmail: string
  adminPassword: string
  companyTaxId?: string
  defaultLanguage: 'en' | 'es'
}

/** Normalized RFC 7807 failure with the stable backend code preserved. */
export class TenantApiError extends Error {
  readonly status: number
  readonly code?: string

  constructor(status: number, title: string, detail: string, code?: string) {
    super(detail || title)
    this.name = 'TenantApiError'
    this.status = status
    this.code = code
  }
}

interface ProblemBody {
  title?: string
  detail?: string
  code?: string
}

function fail(error: unknown): never {
  if (axios.isAxiosError<ProblemBody>(error)) {
    const status = error.response?.status ?? 0
    const body = error.response?.data
    throw new TenantApiError(
      status,
      body?.title ?? error.message,
      body?.detail ?? error.message,
      body?.code,
    )
  }

  throw error
}

export async function fetchTenantBySubdomain(subdomain: string): Promise<TenantLookup> {
  try {
    const { data } = await publicClient.get<TenantLookup>(
      `/v1/tenants/by-subdomain/${encodeURIComponent(subdomain)}`,
    )
    return data
  } catch (error) {
    return fail(error)
  }
}

export async function checkSubdomainAvailability(subdomain: string): Promise<boolean> {
  try {
    const { data } = await publicClient.get<{ subdomain: string; available: boolean }>(
      '/v1/tenants/check-availability',
      { params: { subdomain } },
    )
    return data.available
  } catch (error) {
    return fail(error)
  }
}

export async function registerTenant(
  payload: RegisterTenantPayload,
): Promise<TenantRegistrationResult> {
  try {
    const { data } = await publicClient.post<TenantRegistrationResult>(
      '/v1/tenants/register',
      payload,
    )
    return data
  } catch (error) {
    return fail(error)
  }
}
