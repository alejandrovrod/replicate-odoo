/** Mirrors Erp.Application.DTOs.AccountTreeNodeDto (decision C7 nested shape). */
export type AccountRootType = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense'

export type AccountType =
  | 'Other'
  | 'Bank'
  | 'Cash'
  | 'Receivable'
  | 'Payable'
  | 'Stock'
  | 'COGS'
  | 'Tax'
  | 'Equity'
  | 'Revenue'
  | 'Expense'
  | 'Depreciation'
  | 'RoundOff'

export interface AccountTreeNode {
  id: string
  code: string
  name: string
  rootType: AccountRootType
  type?: AccountType
  isGroup: boolean
  isActive: boolean
  children: AccountTreeNode[]
  /** Base64 rowversion token from GET; echoed back on PUT for optimistic concurrency. */
  rowVersion?: string
}

export interface GroupAccountOption {
  id: string
  code: string
  name: string
}

/**
 * Pure presentational helper (COA-01): flattens the tree into group-only parent
 * options. The client pre-filters the parent selector to groups; the domain
 * (`parent_is_not_group`) stays authoritative and re-validates server-side.
 */
export function collectGroupAccounts(nodes: AccountTreeNode[]): GroupAccountOption[] {
  const options: GroupAccountOption[] = []
  const walk = (items: AccountTreeNode[]): void => {
    for (const node of items) {
      if (node.isGroup) {
        options.push({ id: node.id, code: node.code, name: node.name })
        walk(node.children)
      }
    }
  }
  walk(nodes)
  return options
}

/**
 * Mirrors `GeneralLedgerEntryDto` (pinned Task 2.6 contract).
 * Rows arrive ordered by `postingDate` ASC, `id` ASC (chronological audit order).
 * `isCancelled` marks reversal rows; `debit`/`credit` are raw amounts in `accountCurrency`.
 */
export interface GeneralLedgerEntryDto {
  id: number
  postingDate: string
  accountId: string
  accountCode: string
  accountName: string
  rootType: string
  debit: number
  credit: number
  accountCurrency: string
  voucherType: string
  voucherNo: string
  voucherId: string
  partyType: string | null
  partyId: string | null
  costCenterId: string | null
  isCancelled: boolean
  remarks: string | null
  createdAt: string
}

/**
 * Mirrors `GeneralLedgerReportDto` (pinned Task 2.6 contract).
 * `totalDebit` / `totalCredit` / `difference` are computed SERVER-SIDE over the FULL filtered
 * set, so they stay correct even when `items` is truncated by the `take` limit (default 500).
 */
export interface GeneralLedgerReportDto {
  items: GeneralLedgerEntryDto[]
  totalDebit: number
  totalCredit: number
  difference: number
}
