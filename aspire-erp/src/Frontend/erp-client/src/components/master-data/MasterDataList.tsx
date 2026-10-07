import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '../ui/Table'
import { Button } from '../ui/Button'
import { Input } from '../ui/Input'
import { Pagination } from '../ui/Pagination'

export interface ColumnDef<T> {
  header: string
  accessor: keyof T | ((row: T) => ReactNode)
}

export interface ListPagination {
  totalCount: number
  page: number
  pageSize: number
  onPageChange: (page: number) => void
}

interface MasterDataListProps<T> {
  title: string
  subtitle?: string
  data: T[]
  columns: ColumnDef<T>[]
  onNew: () => void
  onRowClick?: (row: T) => void
  searchPlaceholder?: string
  newButtonText?: string
  /**
   * Standard Pagination Pattern: pass the backend envelope metadata and the list renders
   * the universal paginator below the table. Omitted for tree views (account tree,
   * warehouse tree), which keep the full hierarchy by design.
   */
  pagination?: ListPagination
  actions?: ReactNode
}

export function MasterDataList<T>({
  title,
  subtitle,
  data,
  columns,
  onNew,
  onRowClick,
  searchPlaceholder = 'Search...',
  newButtonText = 'New',
  pagination,
  actions,
}: MasterDataListProps<T>) {
  // Shared chrome lives in the `common` namespace so this generic list needs no ns prop.
  const { t } = useTranslation('common')
  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-slate-900">
            {title}
          </h2>
          {subtitle && <p className="text-sm text-slate-500">{subtitle}</p>}
        </div>
        <div className="flex items-center gap-3">
          {actions}
          <Button onClick={onNew}>{newButtonText}</Button>
        </div>
      </div>

      <div className="flex items-center space-x-2">
        <Input placeholder={searchPlaceholder} className="max-w-sm" />
      </div>

      <div className="bg-white">
        <Table>
          <TableHeader>
            <TableRow>
              {columns.map((col, index) => (
                <TableHead key={index}>{col.header}</TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {data.length === 0 ? (
              <TableRow>
                <TableCell
                  colSpan={columns.length}
                  className="h-24 text-center"
                >
                  {t('state.empty')}
                </TableCell>
              </TableRow>
            ) : (
              data.map((row, rowIndex) => (
                <TableRow
                  key={rowIndex}
                  onClick={() => onRowClick && onRowClick(row)}
                  className={onRowClick ? 'cursor-pointer' : ''}
                >
                  {columns.map((col, colIndex) => (
                    <TableCell key={colIndex}>
                      {typeof col.accessor === 'function'
                        ? col.accessor(row)
                        : (row[col.accessor] as ReactNode)}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
      </div>

      {pagination && pagination.totalCount > 0 ? (
        <div className="flex justify-end">
          <Pagination
            totalCount={pagination.totalCount}
            page={pagination.page}
            pageSize={pagination.pageSize}
            onPageChange={pagination.onPageChange}
          />
        </div>
      ) : null}
    </div>
  )
}
