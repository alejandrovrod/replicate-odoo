/**
 * Strict client mirror of the Block B CRM read models (`LeadDto`, `OpportunityDto`,
 * `IngestLeadResultDto`, `ConvertLeadResultDto`). Property names are the camelCase JSON
 * the API emits; stage/status strings are the backend `OpportunityStage` /
 * `OpportunityStatus` constants verbatim.
 */

export type OpportunityStage =
  | 'Prospecting'
  | 'Qualification'
  | 'Proposal'
  | 'Negotiation'
  | 'ClosedWon'
  | 'ClosedLost'

export type OpportunityStatus = 'Open' | 'Won' | 'Lost' | 'Expired'

/** Mirrors `OpportunityDto` (GET /api/v1/opportunities, the pipeline board source). */
export interface OpportunityDto {
  id: string
  companyId: string
  opportunityNumber: string
  partyName: string
  stage: OpportunityStage
  status: OpportunityStatus
  opportunityAmount: number
  probability: number
  weightedAmount: number
  /** RM-09 FK to the currency catalog (null = legacy "USD" fallback). */
  currencyId?: string | null
  lossReason: string | null
  /** Optimistic token as base64; echoed back verbatim on advance (CRM-06). */
  rowVersion: string
}

/** Mirrors `LeadDto` (GET /api/v1/leads). */
export interface LeadDto {
  id: string
  companyId: string
  leadCode: string
  leadName: string
  organizationName: string | null
  email: string | null
  phone: string | null
  source: string
  status: string
}

/** Mirrors `IngestLeadResultDto` (POST /api/v1/leads/ingest). */
export interface IngestLeadResult {
  lead: LeadDto
  /** True when the (CompanyId, Source, DeduplicationKey) triple already existed. */
  duplicate: boolean
}

/** Mirrors `ConvertLeadResultDto` (POST /api/v1/leads/{id}/convert). */
export interface ConvertLeadResult {
  leadId: string
  customerId: string
  customerCode: string
  opportunityId: string
  opportunityNumber: string
  weightedPipelineAmount: number
}

/** Body of POST /api/v1/opportunities/{id}/advance (the Kanban drag-drop write). */
export interface AdvanceStagePayload {
  id: string
  toStage: OpportunityStage
  probability?: number
  /** Free-text notes (the dialog always sends lossReasonId; raw callers may match by text). */
  lossReason?: string | null
  /** Normalized catalog reason, mandatory for ClosedLost. */
  lossReasonId?: string | null
  rowVersion?: string
}

/** Body of POST /api/v1/opportunities/{id}/create-sales-order (spec CRM-02 1-click creation). */
export interface CreateSalesOrderPayload {
  id: string
  itemId: string
  quantity: number
  rate: number
}

/** Created sales order as returned by the create-sales-order route (header + order number). */
export interface SalesOrderCreated {
  id: string
  orderNumber: string
  customerId: string
  grandTotal: number
  status: string
}

/** One GET /api/v1/items row, trimmed to what the sales-order line picker needs. */
export interface ItemOption {
  id: string
  code: string
  name: string
}

/** One priced deal line (mirrors OpportunityItemDto). */
export interface OpportunityItemDto {
  id: string
  itemId: string
  itemName: string
  quantity: number
  rate: number
  discountPercentage: number
  amount: number
}

/** One follow-up log row of the deal (mirrors OpportunityActivityDto). */
export interface OpportunityActivityDto {
  id: string
  type: string
  subject: string
  content: string | null
  activityDate: string
  nextFollowUpDate: string | null
  createdByUserId: string
}

/** One quotation issued from the deal (mirrors OpportunityQuotationDto). */
export interface OpportunityQuotationDto {
  id: string
  quotationNo: string
  status: string
  totalAmount: number
  transactionDate: string
}

/** Full commercial dossier (mirrors OpportunityDetailDto, the detail drawer source). */
export interface OpportunityDetailDto {
  id: string
  companyId: string
  opportunityNumber: string
  opportunityFrom: string
  partyId: string
  partyName: string
  stage: OpportunityStage
  status: OpportunityStatus
  opportunityAmount: number
  probability: number
  weightedAmount: number
  currencyId?: string | null
  currencyCode: string
  exchangeRate: number
  baseOpportunityAmount: number
  expectedClosingDate: string
  territoryId?: string | null
  territoryName?: string | null
  industryId?: string | null
  industryName?: string | null
  marketSegment?: string | null
  competitors: string[]
  lossReasonId?: string | null
  lossReasonName?: string | null
  lossReason: string | null
  assignedSalespersonId?: string | null
  rowVersion: string
  items: OpportunityItemDto[]
  activities: OpportunityActivityDto[]
  quotations: OpportunityQuotationDto[]
}

/** Editable line input of the dossier edit (full-replace semantics). */
export interface OpportunityItemInput {
  itemId: string
  quantity: number
  rate: number
  discountPercentage?: number
}

/** Body of PUT /api/v1/opportunities/{id} (null leaves a field unchanged). */
export interface UpdateOpportunityPayload {
  id: string
  rowVersion: string
  partyName?: string
  probability?: number
  expectedClosingDate?: string
  assignedSalespersonId?: string | null
  currencyId?: string | null
  clearCurrency?: boolean
  exchangeRate?: number
  territoryId?: string | null
  industryId?: string | null
  marketSegment?: string | null
  competitors?: string[]
  items?: OpportunityItemInput[] | null
}

/** One row of the read-only segmentation catalogs. */
export interface CrmCatalogDto {
  id: string
  code: string
  name: string
}

/** Formal quotation as returned by the create-quotation route (header + lines). */
export interface QuotationDto {
  id: string
  companyId: string
  quotationNo: string
  opportunityId?: string | null
  customerId: string
  transactionDate: string
  validTill: string
  status: string
  totalAmount: number
  currency: string
  exchangeRate: number
  createdAt: string
  items: OpportunityItemDto[]
}

/** Body of POST /api/v1/opportunities/{id}/create-quotation. */
export interface CreateQuotationPayload {
  id: string
  validTill?: string | null
  termsAndConditions?: string | null
  lines?: { itemId: string; quantity: number; rate: number }[] | null
}
