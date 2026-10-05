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
  currency: string
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
  lossReason?: string
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
