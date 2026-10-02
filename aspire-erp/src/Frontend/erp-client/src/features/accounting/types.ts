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
