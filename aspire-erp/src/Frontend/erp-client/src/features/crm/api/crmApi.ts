import { apiClient } from '../../../api/client'
import type { PagedResult } from '../../../lib/pagination'
import { useTenantStore } from '../../../store/useTenantStore'
import type {
  AdvanceStagePayload,
  ConvertLeadResult,
  CreateSalesOrderPayload,
  IngestLeadResult,
  ItemOption,
  LeadDto,
  OpportunityDto,
  SalesOrderCreated,
} from '../types/crm'

/**
 * Live CRM API surface (Block B routes - `LeadsController` / `OpportunitiesController`).
 * No mock data: every call hits the real endpoint. The shared `apiClient` injects
 * `X-Tenant-ID` and mints a fresh `Idempotency-Key` per POST (Constitution VI.4), so
 * callers pass only business parameters.
 */
export const crmApi = {
  /** GET /api/v1/opportunities - the pipeline board source, newest first (paginated). */
  getOpportunities: async (stage?: string, page = 1, pageSize = 50): Promise<PagedResult<OpportunityDto>> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.get<PagedResult<OpportunityDto>>('/v1/opportunities', {
      params: { companyId, page, pageSize, ...(stage ? { stage } : {}) },
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

  /** GET /api/v1/leads - the company's most recent leads (paginated). */
  getLeads: async (page = 1, pageSize = 50): Promise<PagedResult<LeadDto>> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.get<PagedResult<LeadDto>>('/v1/leads', {
      params: { companyId, page, pageSize },
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

  /** POST /api/v1/opportunities/{id}/create-sales-order - 1-click order from a ClosedWon deal. */
  createSalesOrder: async (payload: CreateSalesOrderPayload): Promise<SalesOrderCreated> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.post<SalesOrderCreated>(
      `/v1/opportunities/${payload.id}/create-sales-order`,
      {
        itemId: payload.itemId,
        quantity: payload.quantity,
        rate: payload.rate,
      },
      { params: { companyId } },
    )
    return response.data
  },

  /** GET /api/v1/items - catalog picker for the sales-order line (bounded single page, no pager). */
  getItems: async (): Promise<ItemOption[]> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.get<PagedResult<ItemOption>>('/v1/items', {
      params: { companyId, page: 1, pageSize: 500 },
    })
    return response.data.items
  },
}
