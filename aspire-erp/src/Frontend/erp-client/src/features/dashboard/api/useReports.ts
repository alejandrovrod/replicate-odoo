import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../../api/client'
import type { QueryStatus } from '../../../lib/useApiList'

export interface FinancialSectionRow {
  accountId: string
  accountCode: string
  accountName: string
  balance: number
}

export interface FinancialSection {
  rows: FinancialSectionRow[]
  total: number
}

export interface TrialBalanceRow {
  accountId: string
  accountCode: string
  accountName: string
  rootType: string
  totalDebit: number
  totalCredit: number
  netBalance: number
}

export interface TrialBalanceReport {
  asOfDate: string
  rows: TrialBalanceRow[]
  totalDebit: number
  totalCredit: number
  difference: number
}

export interface BalanceSheetReport {
  assets: FinancialSection
  liabilities: FinancialSection
  equity: FinancialSection
  balanced: boolean
}

export interface ProfitAndLossReport {
  revenue: FinancialSection
  cogs: FinancialSection
  expenses: FinancialSection
  netProfit: number
}

export interface AgingRow {
  partyType: string
  partyId: string
  partyName: string
  voucherType: string
  voucherNo: string
  postingDate: string
  dueDate: string
  invoicedAmount: number
  paidAmount: number
  outstandingAmount: number
  ageDays: number
  bucket: string
  currency: string
}

export interface AgingLegTotals {
  outstanding: number
  notDue: number
  range030: number
  range3160: number
  range6190: number
  range90Plus: number
}

export interface AgingReport {
  companyId: string
  reportDate: string
  rows: AgingRow[]
  totals: { receivable: AgingLegTotals; payable: AgingLegTotals }
}

export interface StockLedgerRow {
  itemId: string
  itemCode: string
  itemName: string
  warehouseId: string
  warehouseCode: string
  postingDate: string
  voucherType: string
  voucherNo: string
  inQty: number
  outQty: number
  balanceQty: number
  valuationRate: number
  balanceValue: number
  valueChange: number
  isOpening: boolean
}

export interface StockLedgerReport {
  companyId: string
  from: string
  to: string
  rows: StockLedgerRow[]
  totalInQty: number
  totalOutQty: number
  totalValueChange: number
}

interface ReportState<T> {
  data: T | null
  status: QueryStatus
  error: ApiError | null
}

const toApiError = (cause: unknown): ApiError =>
  cause instanceof ApiError
    ? cause
    : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause))

/**
 * Single-object GET loader for the read-only financial reports (tasks.md 7.1): same
 * status/error/reload contract as `useApiList`, minus the paged envelope (a statement is
 * one document, not a page). Tenant header injection and RFC 7807 handling live in
 * `src/api/client.ts`.
 */
export function useReport<T>(
  path: string,
  params: Record<string, string | number | boolean | undefined>,
  enabled: boolean,
): { data: T | null; status: QueryStatus; error: ApiError | null; reload: () => void } {
  const paramsKey = JSON.stringify(params)
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<ReportState<T> | null>(null)

  useEffect(() => {
    if (!enabled) return undefined
    let cancelled = false
    const query = JSON.parse(paramsKey) as Record<string, string | number | boolean>
    apiClient.get<T>(path, { params: query }).then(
      (response) => {
        if (!cancelled) setState({ data: response.data, status: 'success', error: null })
      },
      (cause: unknown) => {
        if (!cancelled) setState({ data: null, status: 'error', error: toApiError(cause) })
      },
    )
    return () => {
      cancelled = true
    }
  }, [path, paramsKey, attempt, enabled])

  const reload = useCallback(() => {
    setState(null)
    setAttempt((current) => current + 1)
  }, [])

  if (!enabled) return { data: null, status: 'idle', error: null, reload }
  if (state) return { ...state, reload }
  return { data: null, status: 'loading', error: null, reload }
}

/** GET /api/v1/FinancialReports/trial-balance?companyId=&asOfDate= */
export function useTrialBalance(companyId: string, asOfDate: string) {
  return useReport<TrialBalanceReport>(
    '/v1/FinancialReports/trial-balance',
    { companyId, asOfDate },
    Boolean(companyId && asOfDate),
  )
}

/** GET /api/v1/FinancialReports/balance-sheet?companyId=&asOfDate= */
export function useBalanceSheet(companyId: string, asOfDate: string) {
  return useReport<BalanceSheetReport>(
    '/v1/FinancialReports/balance-sheet',
    { companyId, asOfDate },
    Boolean(companyId && asOfDate),
  )
}

/** GET /api/v1/FinancialReports/profit-and-loss?companyId=&from=&to= */
export function useProfitAndLoss(companyId: string, from: string, to: string) {
  return useReport<ProfitAndLossReport>(
    '/v1/FinancialReports/profit-and-loss',
    { companyId, from, to },
    Boolean(companyId && from && to),
  )
}

/** GET /api/v1/FinancialReports/aging?companyId=&reportDate= */
export function useAgingReport(companyId: string, reportDate: string) {
  return useReport<AgingReport>(
    '/v1/FinancialReports/aging',
    { companyId, reportDate },
    Boolean(companyId && reportDate),
  )
}

/** GET /api/v1/FinancialReports/stock-ledger?companyId=&from=&to= */
export function useStockLedgerReport(companyId: string, from: string, to: string, take = 500) {
  return useReport<StockLedgerReport>(
    '/v1/FinancialReports/stock-ledger',
    { companyId, from, to, take },
    Boolean(companyId && from && to),
  )
}

export function todayIso(): string {
  return new Date().toISOString().slice(0, 10)
}

export function monthStartIso(date = new Date()): string {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-01`
}
