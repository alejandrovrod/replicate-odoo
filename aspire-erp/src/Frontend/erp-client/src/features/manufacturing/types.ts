/**
 * Strict client mirror of `Erp.Application.DTOs.WorkOrderDto` and `BomDto` plus the
 * status-flow action shapes. Property names are the camelCase JSON the API emits
 * (enums as their NAME via `JsonStringEnumConverter`, `DateOnly` as "YYYY-MM-DD").
 */

/** Mirrors `WorkOrderStatus` (Draft -> Submitted -> InProcess -> Completed, Cancelled). */
export type WorkOrderStatus = 'Draft' | 'Submitted' | 'InProcess' | 'Completed' | 'Cancelled'

/** Mirrors `WorkOrderDto` (GET /api/v1/workorders). */
export interface WorkOrder {
  id: string
  companyId: string
  orderNumber: string
  productionItemId: string
  bomId: string
  quantityToProduce: number
  producedQuantity: number
  status: WorkOrderStatus
  sourceWarehouseId: string
  wipWarehouseId: string
  targetWarehouseId: string
  /** DateOnly serializes as "yyyy-MM-dd". */
  plannedStartDate: string
  plannedEndDate: string
  actualStartDate: string | null
  actualEndDate: string | null
  createdAt: string
}

/** Mirrors `BomItemDto` (one component line of GET /api/v1/boms). */
export interface BomItem {
  id: string
  itemId: string
  itemCode: string
  itemName: string
  quantity: number
  valuationRate: number
  amount: number
  scrapPercentage: number
}

/** Mirrors `BomOperationDto` (one workstation step of GET /api/v1/boms). */
export interface BomOperation {
  id: string
  workstationId: string
  workstationName: string
  hourRateTotal: number
  description: string | null
  durationMinutes: number
  operationCost: number
}

/** Mirrors `BomDto` (GET /api/v1/boms + GET /api/v1/boms/{id}). */
export interface Bom {
  id: string
  companyId: string
  bomNumber: string
  itemId: string
  itemCode: string
  itemName: string
  quantity: number
  isActive: boolean
  isDefault: boolean
  rawMaterialCost: number
  operatingCost: number
  scrapCost: number
  totalCost: number
  items: BomItem[]
  operations: BomOperation[]
}

/** Next legal action for a work order in its current status. */
export function nextActions(status: WorkOrderStatus): WorkOrderStatus[] {
  switch (status) {
    case 'Draft':
      return ['Submitted']
    case 'Submitted':
      return ['InProcess', 'Cancelled']
    case 'InProcess':
      return ['Completed', 'Cancelled']
    default:
      return []
  }
}

/**
 * Minimal mirror of `StockEntryPostingDto` as returned by the transfer/complete/cancel
 * postings (structural: the wire payload carries more fields, which callers ignore).
 */
export interface ManufacturingPosting {
  entry: {
    id: string
    voucherNo: string
    entryType: string
  }
  totalDebit: number
  totalCredit: number
}
