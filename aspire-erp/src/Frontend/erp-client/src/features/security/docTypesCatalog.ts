/**
 * DocType catalog mirror (triad spec §5 — Role & Permission Manager).
 *
 * Only DocTypes real to the backend `SecurityRoleSeeder` are listed here. The triad
 * defines NO catalog endpoint, so this constant is the source of truth on the client
 * (see `getDocTypesCatalogApi` in `securityApi.ts`). If the backend ever exposes a
 * catalog, that fetcher can prefer the wire copy and fall back to this one.
 *
 * Actions: Read | Write | Create | Delete | Submit | Cancel.
 * Semantics (spec §5): `Create ⇒ Write`; `Delete = false` by default except
 * System Manager (which bypasses everything and owns no permission rows).
 */

export type PermissionAction = 'read' | 'write' | 'create' | 'delete' | 'submit' | 'cancel'

export const PERMISSION_ACTIONS: readonly PermissionAction[] = [
  'read',
  'write',
  'create',
  'delete',
  'submit',
  'cancel',
]

export type DocTypeModule =
  | 'sales'
  | 'purchase'
  | 'accounting'
  | 'stock'
  | 'core'
  | 'hr'
  | 'manufacturing'

export interface DocTypeInfo {
  docType: string
  /** Human-readable label used in the matrix and in per-cell aria-labels. */
  displayName: string
  module: DocTypeModule
}

export const DOC_TYPE_MODULES: readonly { id: DocTypeModule; label: string }[] = [
  { id: 'sales', label: 'Sales' },
  { id: 'purchase', label: 'Purchasing' },
  { id: 'accounting', label: 'Accounting' },
  { id: 'stock', label: 'Stock & Items' },
  { id: 'core', label: 'Core & Catalog' },
  { id: 'hr', label: 'Human Resources' },
  { id: 'manufacturing', label: 'Manufacturing & Projects' },
]

export const DOC_TYPES_CATALOG: readonly DocTypeInfo[] = [
  // Sales
  { docType: 'sales_order', displayName: 'Sales Order', module: 'sales' },
  { docType: 'sales_invoice', displayName: 'Sales Invoice', module: 'sales' },
  { docType: 'delivery_note', displayName: 'Delivery Note', module: 'sales' },
  { docType: 'customer', displayName: 'Customer', module: 'sales' },
  { docType: 'lead', displayName: 'Lead', module: 'sales' },
  { docType: 'opportunity', displayName: 'Opportunity', module: 'sales' },
  // Stock / Item
  { docType: 'item', displayName: 'Item', module: 'stock' },
  { docType: 'warehouse', displayName: 'Warehouse', module: 'stock' },
  { docType: 'stock_entry', displayName: 'Stock Entry', module: 'stock' },
  { docType: 'stock', displayName: 'Stock Balance', module: 'stock' },
  // Catalog / Core
  { docType: 'company', displayName: 'Company', module: 'core' },
  { docType: 'currency', displayName: 'Currency', module: 'core' },
  { docType: 'catalog', displayName: 'Catalog', module: 'core' },
  // Purchase
  { docType: 'purchase_order', displayName: 'Purchase Order', module: 'purchase' },
  { docType: 'purchase_invoice', displayName: 'Purchase Invoice', module: 'purchase' },
  { docType: 'purchase_receipt', displayName: 'Purchase Receipt', module: 'purchase' },
  { docType: 'supplier', displayName: 'Supplier', module: 'purchase' },
  // Accounting
  { docType: 'account', displayName: 'Account', module: 'accounting' },
  { docType: 'journal_entry', displayName: 'Journal Entry', module: 'accounting' },
  { docType: 'payment_entry', displayName: 'Payment Entry', module: 'accounting' },
  { docType: 'gl_entry', displayName: 'GL Entry', module: 'accounting' },
  { docType: 'bank_account', displayName: 'Bank Account', module: 'accounting' },
  { docType: 'bank_transaction', displayName: 'Bank Transaction', module: 'accounting' },
  { docType: 'bank_statement_import', displayName: 'Bank Statement Import', module: 'accounting' },
  { docType: 'bank_transaction_rule', displayName: 'Bank Transaction Rule', module: 'accounting' },
  { docType: 'fiscal_year', displayName: 'Fiscal Year', module: 'accounting' },
  { docType: 'period_closing_voucher', displayName: 'Period Closing Voucher', module: 'accounting' },
  { docType: 'exchange_rate', displayName: 'Exchange Rate', module: 'accounting' },
  { docType: 'exchange_rate_revaluation', displayName: 'Exchange Rate Revaluation', module: 'accounting' },
  { docType: 'asset', displayName: 'Asset', module: 'accounting' },
  { docType: 'asset_category', displayName: 'Asset Category', module: 'accounting' },
  { docType: 'report', displayName: 'Report', module: 'accounting' },
  // HR
  { docType: 'employee', displayName: 'Employee', module: 'hr' },
  { docType: 'attendance', displayName: 'Attendance', module: 'hr' },
  { docType: 'leave_application', displayName: 'Leave Application', module: 'hr' },
  { docType: 'salary_component', displayName: 'Salary Component', module: 'hr' },
  { docType: 'salary_structure', displayName: 'Salary Structure', module: 'hr' },
  { docType: 'payroll_entry', displayName: 'Payroll Entry', module: 'hr' },
  // Manufacturing / Projects
  { docType: 'bom', displayName: 'Bill of Materials', module: 'manufacturing' },
  { docType: 'work_order', displayName: 'Work Order', module: 'manufacturing' },
  { docType: 'project', displayName: 'Project', module: 'manufacturing' },
]

export function displayNameFor(docType: string): string {
  return DOC_TYPES_CATALOG.find((d) => d.docType === docType)?.displayName ?? docType
}
