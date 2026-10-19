import { apiClient } from '../../../api/client'
import type { PagedResult } from '../../../lib/pagination'
import { useTenantStore } from '../../../store/useTenantStore'
import type {
  AdvanceStagePayload,
  ConvertLeadResult,
  CreateQuotationPayload,
  CreateSalesOrderPayload,
  CrmCatalogDto,
  IngestLeadResult,
  ItemOption,
  LeadDto,
  OpportunityDetailDto,
  OpportunityDto,
  QuotationDto,
  SalesOrderCreated,
  UpdateOpportunityPayload,
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
   * ClosedWon forces 100%, ClosedLost forces 0% and requires `lossReasonId` (the
   * normalized catalog reason; `lossReason` carries only the additional notes); a stale
   * `rowVersion` fails with 409 `concurrency_conflict` (spec CRM-06).
   */
  advanceStage: async (payload: AdvanceStagePayload, signal?: AbortSignal): Promise<OpportunityDto> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.post<OpportunityDto>(
      `/v1/opportunities/${payload.id}/advance`,
      {
        toStage: payload.toStage,
        probability: payload.probability ?? null,
        lossReason: payload.lossReason ?? null,
        lossReasonId: payload.lossReasonId ?? null,
      },
      {
        params: {
          companyId,
          ...(payload.rowVersion ? { rowVersion: payload.rowVersion } : {}),
        },
        signal,
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
      defaultCurrencyId?: string | null
      paymentTermsDays?: number
      opportunityAmount?: number
      opportunityProbability?: number
      expectedClosingDate?: string | null
    },
  ): Promise<ConvertLeadResult> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.post<ConvertLeadResult>(
      `/v1/leads/${id}/convert`,
      { defaultCurrencyId: null, paymentTermsDays: 30, ...body },
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
  getItems: async (signal?: AbortSignal): Promise<ItemOption[]> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.get<PagedResult<ItemOption>>('/v1/items', {
      params: { companyId, page: 1, pageSize: 500 },
      signal,
    })
    return response.data.items
  },

  /** GET /api/v1/opportunities/{id} - the full commercial dossier (detail drawer source). */
  getOpportunityDetail: async (id: string, signal?: AbortSignal): Promise<OpportunityDetailDto> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.get<OpportunityDetailDto>(`/v1/opportunities/${id}`, {
      params: { companyId },
      signal,
    })
    return response.data
  },

  /** PUT /api/v1/opportunities/{id} - dossier edit (null leaves a field unchanged). */
  updateOpportunity: async (payload: UpdateOpportunityPayload, signal?: AbortSignal): Promise<OpportunityDetailDto> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.put<OpportunityDetailDto>(
      `/v1/opportunities/${payload.id}`,
      {
        opportunityId: payload.id,
        companyId,
        rowVersion: payload.rowVersion,
        ...(payload.partyName !== undefined ? { partyName: payload.partyName } : {}),
        ...(payload.probability !== undefined ? { probability: payload.probability } : {}),
        ...(payload.expectedClosingDate !== undefined ? { expectedClosingDate: payload.expectedClosingDate } : {}),
        ...(payload.assignedSalespersonId !== undefined ? { assignedSalespersonId: payload.assignedSalespersonId } : {}),
        ...(payload.currencyId !== undefined ? { currencyId: payload.currencyId } : {}),
        ...(payload.clearCurrency ? { clearCurrency: true } : {}),
        ...(payload.exchangeRate !== undefined ? { exchangeRate: payload.exchangeRate } : {}),
        ...(payload.territoryId !== undefined ? { territoryId: payload.territoryId } : {}),
        ...(payload.industryId !== undefined ? { industryId: payload.industryId } : {}),
        ...(payload.marketSegment !== undefined ? { marketSegment: payload.marketSegment } : {}),
        ...(payload.competitors !== undefined ? { competitors: payload.competitors } : {}),
        ...(payload.items !== undefined ? { items: payload.items } : {}),
      },
      { params: { companyId }, signal },
    )
    return response.data
  },

  /** POST /api/v1/opportunities/{id}/create-quotation - Draft quotation from a ClosedWon deal. */
  createQuotation: async (payload: CreateQuotationPayload, signal?: AbortSignal): Promise<QuotationDto> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.post<QuotationDto>(
      `/v1/opportunities/${payload.id}/create-quotation`,
      {
        validTill: payload.validTill ?? null,
        termsAndConditions: payload.termsAndConditions ?? null,
        lines: payload.lines ?? null,
      },
      { params: { companyId }, signal },
    )
    return response.data
  },

  /** GET /api/v1/quotations/{id} - one formal quotation with its priced lines. */
  getQuotation: async (id: string, signal?: AbortSignal): Promise<QuotationDto> => {
    const companyId = useTenantStore.getState().companyId
    const response = await apiClient.get<QuotationDto>(`/v1/quotations/${id}`, {
      params: { companyId },
      signal,
    })
    return response.data
  },

  /** GET /api/v1/loss-reasons - normalized close reasons (ClosedLost dialog source). */
  getLossReasons: async (signal?: AbortSignal): Promise<CrmCatalogDto[]> => {
    const response = await apiClient.get<CrmCatalogDto[]>('/v1/loss-reasons', { signal })
    return response.data
  },

  /** GET /api/v1/territories - segmentation dropdown source. */
  getTerritories: async (signal?: AbortSignal): Promise<CrmCatalogDto[]> => {
    const response = await apiClient.get<CrmCatalogDto[]>('/v1/territories', { signal })
    return response.data
  },

  /** GET /api/v1/industries - segmentation dropdown source. */
  getIndustries: async (signal?: AbortSignal): Promise<CrmCatalogDto[]> => {
    const response = await apiClient.get<CrmCatalogDto[]>('/v1/industries', { signal })
    return response.data
  },
}
