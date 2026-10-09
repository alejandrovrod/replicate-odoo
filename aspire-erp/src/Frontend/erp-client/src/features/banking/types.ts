/**
 * Strict client mirror of `Erp.Application.DTOs.BankingDto` plus the rule-run and
 * reconcile result shapes. Property names are the camelCase JSON the API emits.
 */

export type BankTransactionStatus = 'Unreconciled' | 'Matched' | 'Reconciled' | 'Excluded'

/** Mirrors `BankTransactionDto` (GET /api/v1/bank-transactions). */
export interface BankTransaction {
  id: string
  companyId: string
  bankAccountId: string
  /** DateOnly serializes as "yyyy-MM-dd". */
  transactionDate: string
  deposit: number
  withdrawal: number
  currency: string
  description: string
  referenceNumber: string | null
  /** Bank external id (OFX FITID): the BN-05 de-duplication key. */
  transactionId: string | null
  status: BankTransactionStatus
  allocatedAmount: number
  clearanceDate: string | null
  suggestedPartyType: string | null
  suggestedPartyId: string | null
  suggestedAccountId: string | null
  /** Optimistic token as base64; echoed back verbatim on reconcile/quick-voucher (BN-07). */
  rowVersion: string | null
  unallocatedAmount: number
  transactionType: string
  partyType: string | null
  partyId: string | null
  bankPartyName: string
  bankPartyAccountNumber: string
  bankPartyIban: string
  isRuleEvaluated: boolean
  matchedTransactionRuleId: string | null
  includedFee: number
  excludedFee: number
}

/** Mirrors `BankStatementImportDto` (POST /api/v1/bank-statement-imports, BN-05 summary). */
export interface BankStatementImportSummary {
  importId: string
  fileName: string
  totalTransactions: number
  importedCount: number
  duplicateCount: number
}

/** Mirrors `BankTransactionRuleDto` (GET /api/v1/bank-transaction-rules). */
export interface BankTransactionRule {
  id: string
  companyId: string
  ruleName: string
  priority: number
  bankAccountId: string | null
  conditionType: string
  pattern: string
  targetPartyType: string | null
  targetPartyId: string | null
  autoCreateVoucher: boolean
  targetExpenseAccountId: string | null
  isActive: boolean
}

/** Mirrors `RuleMatchOutcome` (POST /api/v1/bank-transactions/run-rules). */
export interface RuleMatchOutcome {
  bankTransactionId: string
  matched: boolean
  ruleId: string | null
  ruleName: string | null
  requiresVoucherCreation: boolean
}

/** Mirrors `RuleMatchSummary`. */
export interface RuleMatchSummary {
  matchedCount: number
  outcomes: RuleMatchOutcome[]
}

/** Mirrors `ReconciledLine` (one persisted allocation slice). */
export interface ReconciledLine {
  linkId: string
  counterpartType: string
  counterpartId: string
  amount: number
}

/** Mirrors `ReconciliationSummary` (POST .../reconcile). */
export interface ReconciliationSummary {
  bankTransactionId: string
  allocatedAmount: number
  clearanceDate: string
  lines: ReconciledLine[]
}

/** One line of a journal voucher as returned by the API. */
export interface JournalEntryLine {
  accountId: string
  accountCode: string
  accountName: string
  lineNumber: number
  debit: number
  credit: number
  partyType: string | null
  partyId: string | null
  costCenterId: string | null
}

/** Mirrors `JournalEntryDto` (POST .../quick-voucher returns 201 + this). */
export interface JournalEntry {
  id: string
  companyId: string
  voucherNo: string
  postingDate: string
  type: string
  status: string
  userRemark: string | null
  createdAt: string
  rowVersion: string
  lines: JournalEntryLine[]
}

/** Net amount of a staging line: |Deposit - Withdrawal| (invariant BN-04). */
export function netAmount(line: BankTransaction): number {
  return Math.abs(line.deposit - line.withdrawal)
}
