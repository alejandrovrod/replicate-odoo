/** Mirrors Erp.Application.DTOs.AccountTreeNodeDto (decision C7 nested shape). */
export type AccountRootType = 'Asset' | 'Liability' | 'Equity' | 'Income' | 'Expense'

export interface AccountTreeNode {
  id: string
  code: string
  name: string
  rootType: AccountRootType
  isGroup: boolean
  isActive: boolean
  children: AccountTreeNode[]
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
