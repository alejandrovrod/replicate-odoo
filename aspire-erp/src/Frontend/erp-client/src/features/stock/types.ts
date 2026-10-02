/**
 * Wire types for the Stock & Inventory endpoints (Task 3.4).
 *
 * Mirrors the C# DTOs in `Erp.Application/DTOs` as serialized by System.Text.Json:
 * camelCase properties, enums emitted as their NAME (`JsonStringEnumConverter` is
 * registered in Program.cs) and `DateOnly` as "YYYY-MM-DD".
 */

export type ValuationMethod = 'Fifo' | 'MovingAverage' | 'Lifo'

export type StockEntryType = 'MaterialReceipt' | 'MaterialIssue' | 'MaterialTransfer'

/** Stock level of one item inside one warehouse (current SUM of the Kardex rows). */
export interface ItemStock {
  warehouseId: string
  warehouseCode: string
  warehouseName: string
  qty: number
  value: number
}

/** GET /api/v1/items row. Items are tenant-wide; `stock` is scoped to the queried company. */
export interface Item {
  id: string
  code: string
  name: string
  valuationMethod: ValuationMethod
  baseUOMId: string
  incomeAccountId: string | null
  expenseAccountId: string | null
  isActive: boolean
  stock: ItemStock[]
}

/** One node of GET /api/v1/warehouses/tree. */
export interface WarehouseNode {
  id: string
  code: string
  name: string
  parentWarehouseId: string | null
  stockAccountId: string
  isGroup: boolean
  isActive: boolean
  children: WarehouseNode[]
}

/** One line of a posted stock voucher (StockEntryLineDto). */
export interface StockEntryLine {
  itemId: string
  itemCode: string
  itemName: string
  qty: number
  rate: number | null
  lineNumber: number
}

/** One Kardex row produced by a posting (StockLedgerEntryDto). */
export interface StockLedgerEntry {
  id: string
  itemId: string
  warehouseId: string
  postingDate: string
  qtyChange: number
  valuationRate: number
  amount: number
}

/** One General Ledger line produced by a posting (GLEntryDto). */
export interface GLEntry {
  id: number
  accountId: string
  accountCode: string
  accountName: string
  postingDate: string
  debit: number
  credit: number
  voucherType: string
  voucherNo: string
  remarks: string | null
}

/** Stock voucher summary (StockEntryDto). */
export interface StockEntry {
  id: string
  companyId: string
  entryType: StockEntryType
  postingDate: string
  voucherNo: string
  warehouseId: string
  targetWarehouseId: string | null
  createdAt: string
  lines: StockEntryLine[]
}

/** POST /api/v1/stockentries 201 payload (StockEntryPostingDto). */
export interface StockEntryPosting {
  entry: StockEntry
  ledgerEntries: StockLedgerEntry[]
  glEntries: GLEntry[]
  totalDebit: number
  totalCredit: number
}

/** One line of POST /api/v1/stockentries (CreateStockEntryLine). */
export interface CreateStockEntryLine {
  itemId: string
  qty: number
  /** Required (and > 0) on receipts only; omitted on issues/transfers (FIFO decides). */
  rate?: number | null
}

/** POST /api/v1/stockentries body (CreateStockEntryCommand). */
export interface CreateStockEntryCommand {
  companyId: string
  entryType: StockEntryType
  warehouseId: string
  targetWarehouseId?: string | null
  /** "YYYY-MM-DD"; omitted/blank falls back to the server's posting date. */
  postingDate?: string
  lines: CreateStockEntryLine[]
}

/** Warehouse flattened out of the tree for selects and table headers. */
export interface FlatWarehouse {
  id: string
  code: string
  name: string
  depth: number
  isGroup: boolean
  isActive: boolean
  stockAccountId: string
}

/**
 * Pre-order flatten of the warehouse tree, carrying depth for indented rendering.
 * `leavesOnly` drops group nodes (they hold no stock) - the set a posting can target.
 */
export function flattenWarehouses(nodes: WarehouseNode[], leavesOnly = false): FlatWarehouse[] {
  const walk = (list: WarehouseNode[], depth: number): FlatWarehouse[] =>
    list.flatMap((node) => {
      const flat: FlatWarehouse = {
        id: node.id,
        code: node.code,
        name: node.name,
        depth,
        isGroup: node.isGroup,
        isActive: node.isActive,
        stockAccountId: node.stockAccountId,
      }
      const children = walk(node.children, depth + 1)
      if (leavesOnly) {
        const isLeaf = !node.isGroup && node.children.length === 0
        return isLeaf ? [flat] : children
      }
      return [flat, ...children]
    })
  return walk(nodes, 0)
}
