import { ChevronDown, ChevronRight, Folder, MinusCircle, Search, X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useTenantStore } from '../../store/useTenantStore'
import type { AccountRootType, AccountTreeNode } from './types'
import { useAccountTree } from './useAccountTree'

/** Depth indent in px. Padding (not margin/width) so column widths never move. */
const INDENT_PX = 20
/** Fixed row height: rows resize identically whether expanded or collapsed (no CLS). */
const ROW_HEIGHT_CLASS = 'h-9'

const ROOT_TYPE_BADGE_CLASS: Record<AccountRootType, string> = {
  Asset: 'bg-sky-100 text-sky-700',
  Liability: 'bg-amber-100 text-amber-800',
  Equity: 'bg-violet-100 text-violet-700',
  Income: 'bg-emerald-100 text-emerald-700',
  Expense: 'bg-rose-100 text-rose-700',
}

interface FlatRow {
  node: AccountTreeNode
  depth: number
  hasChildren: boolean
}

/** Pre-order walk of the *visible* nodes (collapsed subtrees are skipped entirely). */
function flatten(nodes: AccountTreeNode[], expanded: ReadonlySet<string>, depth = 0): FlatRow[] {
  const rows: FlatRow[] = []
  for (const node of nodes) {
    const hasChildren = node.children.length > 0
    rows.push({ node, depth, hasChildren })
    if (hasChildren && expanded.has(node.id)) {
      rows.push(...flatten(node.children, expanded, depth + 1))
    }
  }
  return rows
}

function collectGroupIds(nodes: AccountTreeNode[]): string[] {
  return nodes.flatMap((node) =>
    node.isGroup && node.children.length > 0
      ? [node.id, ...collectGroupIds(node.children)]
      : collectGroupIds(node.children),
  )
}

/** Every node that *renders children* in `nodes` (all of them open up while filtering). */
function collectExpandableIds(nodes: AccountTreeNode[]): string[] {
  return nodes.flatMap((node) =>
    node.children.length > 0
      ? [node.id, ...collectExpandableIds(node.children)]
      : collectExpandableIds(node.children),
  )
}

/** Case-insensitive match against code OR name (`query` must already be lowercased). */
function matchesQuery(node: AccountTreeNode, query: string): boolean {
  return node.code.toLowerCase().includes(query) || node.name.toLowerCase().includes(query)
}

/**
 * Client-side COA filter. A node is kept when it matches OR when a descendant matches, so a
 * match is always visible under its ancestors (context preserved). A matching node keeps its
 * whole subtree (the match is the interesting branch); an ancestor that only leads to a match
 * keeps just the branches that contain one.
 */
function filterTree(nodes: AccountTreeNode[], query: string): AccountTreeNode[] {
  const kept: AccountTreeNode[] = []
  for (const node of nodes) {
    if (matchesQuery(node, query)) {
      kept.push(node)
      continue
    }
    const children = filterTree(node.children, query)
    if (children.length > 0) {
      kept.push({ ...node, children })
    }
  }
  return kept
}

interface FilteredTree {
  nodes: AccountTreeNode[]
  /** Every kept group renders open: ancestors reveal the match, matched subtrees reveal their children. */
  expandedIds: ReadonlySet<string>
}

interface AccountTreeTableProps {
  companyId: string
}

/**
 * Collapsible Chart-of-Accounts tree table (Task 2.4).
 *
 * Zero layout shift: `table-fixed` + `<colgroup>` pin the column widths, every row is a
 * fixed `h-9`, and indentation is padding on the name cell (the flexible column), so
 * expanding or collapsing a branch only adds/removes whole rows, it never reflows columns.
 *
 * The toolbar filter matches code OR name (case-insensitive), keeps ancestors of every
 * match visible, auto-expands matched subtrees, and restores the pre-search expand/collapse
 * state when cleared. Toggling still works while a filter is active.
 */
export function AccountTreeTable({ companyId }: AccountTreeTableProps) {
  const { nodes, status, error, reload } = useAccountTree(companyId)
  const tenantId = useTenantStore((state) => state.tenantId)
  const [query, setQuery] = useState('')
  // `null` = the user has not toggled anything yet, so expansion is derived from the tree:
  // every group starts open (the acceptance case is "hierarchical accounts visible on first
  // paint"). The first toggle materialises a set and the user's choice then wins.
  const [userExpanded, setUserExpanded] = useState<ReadonlySet<string> | null>(null)
  // Expansion overrides made *while filtering* — kept separate so clearing the input can
  // restore `userExpanded` untouched. Reset on every keystroke (a new query re-derives it).
  const [searchExpanded, setSearchExpanded] = useState<ReadonlySet<string> | null>(null)

  const searchQuery = query.trim().toLowerCase()
  const filtered = useMemo<FilteredTree | null>(() => {
    if (!searchQuery) return null
    const filteredNodes = filterTree(nodes, searchQuery)
    return { nodes: filteredNodes, expandedIds: new Set(collectExpandableIds(filteredNodes)) }
  }, [nodes, searchQuery])

  const baseExpanded = useMemo(
    () => userExpanded ?? new Set(collectGroupIds(nodes)),
    [userExpanded, nodes],
  )

  const expanded = useMemo(() => {
    if (filtered === null) return baseExpanded
    if (searchExpanded) return searchExpanded
    // First render of this query: the user's baseline OR the auto-expanded matched branches.
    const merged = new Set(baseExpanded)
    for (const id of filtered.expandedIds) merged.add(id)
    return merged
  }, [filtered, searchExpanded, baseExpanded])

  const rows = useMemo(
    () => flatten(filtered ? filtered.nodes : nodes, expanded),
    [nodes, filtered, expanded],
  )

  const toggle = (id: string) => {
    const next = new Set(expanded)
    if (next.has(id)) next.delete(id)
    else next.add(id)
    if (filtered === null) setUserExpanded(next)
    else setSearchExpanded(next)
  }

  const changeQuery = (value: string) => {
    setQuery(value)
    setSearchExpanded(null)
  }

  if (!companyId || !tenantId) {
    return (
      <p className="rounded-md border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-800">
        No tenant/company selected. Set <code>VITE_TENANT_ID</code> and{' '}
        <code>VITE_COMPANY_ID</code> in <code>.env.development</code>.
      </p>
    )
  }

  if (status === 'loading') {
    return <p className="px-1 py-3 text-sm text-slate-500">Loading chart of accounts…</p>
  }

  if (status === 'error') {
    return (
      <div className="rounded-md border border-rose-300 bg-rose-50 px-4 py-3 text-sm text-rose-800">
        <p className="font-medium">
          Could not load the chart of accounts
          {error?.status ? ` (HTTP ${error.status})` : ''}.
        </p>
        {error?.code ? (
          <p className="mt-1">
            Error code:{' '}
            <code className="rounded bg-rose-100 px-1 py-0.5 font-mono text-xs">{error.code}</code>
          </p>
        ) : null}
        {error?.message ? <p className="mt-1">{error.message}</p> : null}
        <button
          type="button"
          onClick={() => {
            reload()
          }}
          className="mt-2 rounded border border-rose-400 px-2 py-1 text-rose-900 hover:bg-rose-100"
        >
          Retry
        </button>
      </div>
    )
  }

  return (
    <div className="w-full rounded-lg border border-slate-200">
      <div className="flex items-center gap-2 border-b border-slate-200 bg-slate-50 px-3 py-2">
        <Search className="size-4 shrink-0 text-slate-400" aria-hidden="true" />
        <input
          type="text"
          value={query}
          onChange={(event) => changeQuery(event.target.value)}
          placeholder="Filter accounts by code or name"
          aria-label="Filter accounts by code or name"
          className="h-8 min-w-0 flex-1 rounded border border-slate-300 bg-white px-2 text-sm text-slate-900 placeholder:text-slate-400 focus:outline focus:outline-2 focus:outline-indigo-600"
        />
        {query.length > 0 ? (
          <button
            type="button"
            onClick={() => changeQuery('')}
            aria-label="Clear filter"
            className="inline-flex size-7 shrink-0 items-center justify-center rounded text-slate-500 hover:bg-slate-200 hover:text-slate-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-indigo-600"
          >
            <X className="size-4" aria-hidden="true" />
          </button>
        ) : null}
      </div>
      <div className="w-full overflow-x-auto">
        <table className="w-full table-fixed border-collapse text-sm">
          <colgroup>
            <col className="w-10" />
            <col className="w-24" />
            <col />
            <col className="w-28" />
            <col className="w-24" />
            <col className="w-24" />
          </colgroup>
          <thead>
            <tr className="border-b border-slate-200 bg-slate-50 text-left text-xs uppercase tracking-wide text-slate-500">
              <th className="px-2 py-2" aria-label="Expand" />
              <th className="px-2 py-2 font-semibold">Code</th>
              <th className="px-2 py-2 font-semibold">Name</th>
              <th className="px-2 py-2 font-semibold">Root Type</th>
              <th className="px-2 py-2 font-semibold">Kind</th>
              <th className="px-2 py-2 font-semibold">Status</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 ? (
              <tr className={ROW_HEIGHT_CLASS}>
                <td colSpan={6} className="px-4 text-center text-slate-500">
                  {filtered
                    ? `No accounts match \u201C${query.trim()}\u201D.`
                    : 'No accounts yet for this company.'}
                </td>
              </tr>
            ) : (
              rows.map(({ node, depth, hasChildren }) => {
                const isExpanded = expanded.has(node.id)
                return (
                  <tr
                    key={node.id}
                    className={`${ROW_HEIGHT_CLASS} border-b border-slate-100 last:border-b-0 hover:bg-slate-50`}
                  >
                    <td className="px-0 text-center align-middle">
                      {hasChildren ? (
                        <button
                          type="button"
                          onClick={() => toggle(node.id)}
                          aria-expanded={isExpanded}
                          aria-label={`${isExpanded ? 'Collapse' : 'Expand'} ${node.code}`}
                          className="inline-flex size-7 items-center justify-center rounded text-slate-500 hover:bg-slate-200 hover:text-slate-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-indigo-600"
                        >
                          {isExpanded ? (
                            <ChevronDown className="size-4" aria-hidden="true" />
                          ) : (
                            <ChevronRight className="size-4" aria-hidden="true" />
                          )}
                        </button>
                      ) : (
                        <span className="inline-flex size-7 items-center justify-center text-slate-300">
                          <MinusCircle className="size-3.5" aria-hidden="true" />
                        </span>
                      )}
                    </td>
                    <td className="px-2 font-mono text-slate-700">{node.code}</td>
                    <td
                      className="truncate px-2 text-slate-900"
                      title={node.name}
                      style={{ paddingLeft: 8 + depth * INDENT_PX }}
                    >
                      {node.name}
                    </td>
                    <td className="px-2">
                      <span
                        className={`inline-flex items-center rounded px-2 py-0.5 text-xs font-medium ${ROOT_TYPE_BADGE_CLASS[node.rootType] ?? 'bg-slate-100 text-slate-700'}`}
                      >
                        {node.rootType}
                      </span>
                    </td>
                    <td className="px-2 text-slate-600">
                      {node.isGroup ? (
                        <span className="inline-flex items-center gap-1 text-xs">
                          <Folder className="size-3.5" aria-hidden="true" />
                          Group
                        </span>
                      ) : (
                        <span className="text-xs text-slate-500">Account</span>
                      )}
                    </td>
                    <td className="px-2">
                      <span
                        className={`inline-flex items-center gap-1.5 text-xs ${
                          node.isActive ? 'text-emerald-700' : 'text-slate-400'
                        }`}
                      >
                        <span
                          className={`size-2 rounded-full ${
                            node.isActive ? 'bg-emerald-500' : 'bg-slate-300'
                          }`}
                          aria-hidden="true"
                        />
                        {node.isActive ? 'Active' : 'Inactive'}
                      </span>
                    </td>
                  </tr>
                )
              })
            )}
          </tbody>
        </table>
      </div>
    </div>
  )
}
