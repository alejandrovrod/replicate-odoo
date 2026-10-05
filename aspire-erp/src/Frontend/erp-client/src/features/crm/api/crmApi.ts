import { apiClient } from '../../../api/client'
import { useTenantStore } from '../../../store/useTenantStore'
import type {
  AdvanceStagePayload,
  ConvertLeadResult,
  IngestLeadResult,
  LeadDto,
  OpportunityDto,
} from '../types/crm'

/**
 * Live CRM API surface (Block B routes - `LeadsController` / `OpportunitiesController`).
 * No mock data: every call hits the real endpoint. The shared `apiClient` injects
 * `X-Tenant-ID` and mints a fresh `Idempotency-Key` per POST (Constitution VI.4), so
 * callers pass only business parameters.
 */
export const crmApi = {
  /** GET /api/v1/opportunities - the pipeline board source, newest first. */
  getOpportunities: async (stage?: string): Promise<OpportunityDto[]> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.get<OpportunityDto[]>('/v1/opportunities', {
      params: { companyId, limit: 50, ...(stage ? { stage } : {}) },
    })
    return response.data
  },

  /**
   * POST /api/v1/opportunities/{id}/advance - the Kanban drag-drop write.
   * ClosedWon forces 100%, ClosedLost forces 0% and requires `lossReason`; a stale
   * `rowVersion` fails with 409 `concurrency_conflict` (spec CRM-06).
   */
  advanceStage: async (payload: AdvanceStagePayload): Promise<OpportunityDto> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.post<OpportunityDto>(
      `/v1/opportunities/${payload.id}/advance`,
      {
        toStage: payload.toStage,
        probability: payload.probability ?? null,
        lossReason: payload.lossReason ?? null,
      },
      {
        params: {
          companyId,
          ...(payload.rowVersion ? { rowVersion: payload.rowVersion } : {}),
        },
      },
    )
    return response.data
  },

  /** POST /api/v1/opportunities/{id}/reopen - revives a ClosedLost deal to Negotiation. */
  reopenOpportunity: async (id: string, newProbability = 50): Promise<string> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.post<string>(
      `/v1/opportunities/${id}/reopen`,
      {},
      { params: { companyId, newProbability } },
    )
    return response.data
  },

  /** GET /api/v1/leads - the company's most recent leads. */
  getLeads: async (): Promise<LeadDto[]> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.get<LeadDto[]>('/v1/leads', {
      params: { companyId, limit: 50 },
    })
    return response.data
  },

  /** POST /api/v1/leads/ingest - idempotent webhook intake (spec CRM-04). */
  ingestLead: async (body: {
    leadCode: string
    leadName: string
    organizationName?: string | null
    email?: string | null
    phone?: string | null
    source?: string
    deduplicationKey?: string | null
  }): Promise<IngestLeadResult> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.post<IngestLeadResult>('/v1/leads/ingest', {
      companyId,
      ...body,
    })
    return response.data
  },

  /** POST /api/v1/leads/{id}/convert - lead-to-customer conversion (spec CRM-03). */
  convertLead: async (
    id: string,
    body: {
      customerCode: string
      defaultCurrency?: string
      paymentTermsDays?: number
      opportunityAmount?: number
      opportunityProbability?: number
      expectedClosingDate?: string | null
    },
  ): Promise<ConvertLeadResult> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.post<ConvertLeadResult>(
      `/v1/leads/${id}/convert`,
      { defaultCurrency: 'USD', paymentTermsDays: 30, ...body },
      { params: { companyId } },
    )
    return response.data
  },
}
