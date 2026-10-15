import { startTransition, useDeferredValue, useEffect, useId, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../../api/client'
import { Input } from '../../../components/ui/Input'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { cn } from '../../../lib/utils'
import { getBanksApi } from '../api/bankingApi'
import type { BankDto } from '../types'

export interface BankComboboxProps {
  /** Catalog id currently selected (null = free-text mode). */
  valueBankId?: string | null
  /** Current bank display name (catalog official name or manual text). */
  customBankName?: string
  /**
   * Company country used to pre-filter the catalog. Source: `Company.CountryCode`
   * (bank-catalog spec §3 D3); defaults to "MX" when the company row predates that
   * column — read-only filtering, never persisted.
   */
  countryCode?: string
  /** Catalog pick → (bank, bank.name); free text → (null, typed text). */
  onSelect: (bank: BankDto | null, freeText: string) => void
  id?: string
  disabled?: boolean
}

type CatalogStatus = 'loading' | 'success' | 'error'

/** "012 - BBVA México" — the single display shape used in the list and the input. */
export function formatBankOption(bank: BankDto): string {
  return `${bank.code} - ${bank.name}`
}

function matchesQuery(bank: BankDto, q: string): boolean {
  const needle = q.trim().toLowerCase()
  if (!needle) return true
  return (
    bank.name.toLowerCase().includes(needle) ||
    bank.code.toLowerCase().includes(needle) ||
    formatBankOption(bank).toLowerCase().includes(needle)
  )
}

/** Exact pick: full "CODE - Name", official name, or bare 3-digit code (BNK-G2). */
function findExactMatch(banks: BankDto[], text: string): BankDto | null {
  const needle = text.trim().toLowerCase()
  if (!needle) return null
  return (
    banks.find(
      (b) =>
        formatBankOption(b).toLowerCase() === needle ||
        b.name.toLowerCase() === needle ||
        b.code.toLowerCase() === needle,
    ) ?? null
  )
}

const MAX_VISIBLE_OPTIONS = 100

/**
 * Catalog autocomplete with offline-first free-text fallback (BNK-G2/G3).
 *
 * - Typing a cataloged name/code resolves to `onSelect(bank, bank.name)`; any other
 *   text reports `onSelect(null, freeText)` so the modal persists a manual `bankName`
 *   with `bankId: null` (BNK-G3) — the input is never locked to the catalog.
 * - An empty/failed catalog degrades to a plain text field; the error alert carries
 *   the RFC7807 `code` via `translateErrorCode` plus a retry.
 */
export function BankCombobox({
  valueBankId,
  customBankName,
  countryCode = 'MX',
  onSelect,
  id = 'bank-account-bank-select',
  disabled = false,
}: BankComboboxProps) {
  const { t, i18n } = useTranslation('banking')
  const baseId = useId()
  const listboxId = `${id}-listbox`
  const inputId = `${id}-input`

  const [banks, setBanks] = useState<BankDto[]>([])
  const [status, setStatus] = useState<CatalogStatus>('loading')
  const [error, setError] = useState<ApiError | null>(null)
  const [attempt, setAttempt] = useState(0)

  const [query, setQuery] = useState(customBankName ?? '')
  const [open, setOpen] = useState(false)
  const [activeIndex, setActiveIndex] = useState(-1)
  const inputRef = useRef<HTMLInputElement>(null)
  const blurTimer = useRef<number | null>(null)

  // Tracks the last externally-driven (modal reset / initialData) state so user
  // keystrokes — which the modal echoes back through `customBankName` — are not
  // mistaken for a reset.
  const lastExternal = useRef(`${valueBankId ?? ''}|${customBankName ?? ''}`)

  useEffect(() => {
    let cancelled = false
    setStatus('loading')
    setError(null)
    getBanksApi(countryCode).then(
      (items) => {
        if (!cancelled) {
          setBanks(items)
          setStatus('success')
        }
      },
      (cause: unknown) => {
        if (!cancelled) {
          setBanks([])
          setStatus('error')
          setError(
            cause instanceof ApiError
              ? cause
              : new ApiError(0, 'Catalog Error', cause instanceof Error ? cause.message : String(cause)),
          )
        }
      },
    )
    return () => {
      cancelled = true
    }
  }, [countryCode, attempt])

  // Adopt external state on modal open/reset and pre-select by id (or by exact name
  // for pre-catalog rows that only carry `bankName`).
  useEffect(() => {
    const key = `${valueBankId ?? ''}|${customBankName ?? ''}`
    if (key === lastExternal.current) return
    lastExternal.current = key
    setQuery(customBankName ?? '')
    setActiveIndex(-1)
  }, [valueBankId, customBankName])

  useEffect(() => {
    return () => {
      if (blurTimer.current !== null) window.clearTimeout(blurTimer.current)
    }
  }, [])

  // Client-side predictive filter (BNK-G2); deferred + transition keep typing fluid
  // against large catalogs.
  const deferredQuery = useDeferredValue(query)
  const filtered = useMemo(() => {
    const matches = banks.filter((b) => matchesQuery(b, deferredQuery))
    return matches.slice(0, MAX_VISIBLE_OPTIONS)
  }, [banks, deferredQuery])

  const selectedBank = valueBankId ? (banks.find((b) => b.id === valueBankId) ?? null) : null

  const commitFreeText = (text: string) => {
    lastExternal.current = `|${text}`
    onSelect(null, text)
  }

  const commitSelection = (bank: BankDto) => {
    lastExternal.current = `${bank.id}|${bank.name}`
    setQuery(bank.name)
    setActiveIndex(-1)
    setOpen(false)
    onSelect(bank, bank.name)
  }

  const handleChange = (value: string) => {
    setQuery(value)
    setOpen(true)
    setActiveIndex(-1)
    // A fully-typed catalog key ("012", official name, "012 - Name") resolves to the
    // entry; anything else is free text with no bank (BNK-G2/G3).
    startTransition(() => {
      const exact = findExactMatch(banks, value)
      if (exact) {
        lastExternal.current = `${exact.id}|${exact.name}`
        onSelect(exact, exact.name)
      } else {
        commitFreeText(value)
      }
    })
  }

  const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
      e.preventDefault()
      if (!open) {
        setOpen(true)
        setActiveIndex(e.key === 'ArrowDown' ? 0 : filtered.length - 1)
        return
      }
      if (filtered.length === 0) return
      setActiveIndex((prev) => {
        const delta = e.key === 'ArrowDown' ? 1 : -1
        const next = prev + delta
        if (next < 0) return filtered.length - 1
        if (next >= filtered.length) return 0
        return next
      })
    } else if (e.key === 'Enter') {
      if (open && activeIndex >= 0 && activeIndex < filtered.length) {
        e.preventDefault()
        commitSelection(filtered[activeIndex])
      } else if (open) {
        const exact = findExactMatch(banks, query)
        if (exact) {
          e.preventDefault()
          commitSelection(exact)
        } else {
          // Explicit commit of a non-cataloged name → manual mode (BNK-G3).
          setOpen(false)
        }
      }
    } else if (e.key === 'Escape') {
      e.preventDefault()
      setOpen(false)
      setActiveIndex(-1)
    }
  }

  const handleBlur = () => {
    // Deferred so an option click (mousedown → select) wins over the blur close.
    blurTimer.current = window.setTimeout(() => {
      setOpen(false)
      setActiveIndex(-1)
    }, 120)
  }

  const handleClear = () => {
    setQuery('')
    setActiveIndex(-1)
    commitFreeText('')
    inputRef.current?.focus()
  }

  const activeOptionId =
    open && activeIndex >= 0 && activeIndex < filtered.length
      ? `${baseId}-option-${activeIndex}`
      : undefined

  const errorMessage = error
    ? translateErrorCode(i18n, error.code, error.message) || error.message
    : ''

  return (
    <div data-testid={id}>
      <div className="relative">
        <Input
          ref={inputRef}
          id={inputId}
          data-testid={`${id}-input`}
          role="combobox"
          aria-autocomplete="list"
          aria-expanded={open}
          aria-controls={listboxId}
          aria-activedescendant={activeOptionId}
          autoComplete="off"
          value={query}
          disabled={disabled}
          placeholder={t('bankCatalog.searchPlaceholder', 'e.g. 012 or BBVA')}
          onChange={(e) => handleChange(e.target.value)}
          onKeyDown={handleKeyDown}
          onFocus={() => setOpen(true)}
          onBlur={handleBlur}
          required
        />
        {query && !disabled && (
          <button
            type="button"
            aria-label={t('bankCatalog.clearSelection', 'Clear bank selection')}
            data-testid={`${id}-clear`}
            onClick={handleClear}
            className="absolute right-2 top-1/2 -translate-y-1/2 rounded px-1 text-slate-400 hover:text-slate-700 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-slate-300"
          >
            ×
          </button>
        )}
      </div>

      {selectedBank && (
        <div className="mt-1 text-xs text-slate-500" data-testid={`${id}-selected`}>
          {formatBankOption(selectedBank)}
        </div>
      )}

      {open && status === 'loading' && (
        <div className="mt-1 space-y-1" aria-hidden="true" data-testid={`${id}-loading`}>
          {[0, 1, 2].map((i) => (
            <div key={i} className="h-9 animate-pulse rounded-md bg-slate-100" />
          ))}
          <span className="sr-only">{t('bankCatalog.loading', 'Loading bank catalog…')}</span>
        </div>
      )}

      {status === 'error' && (
        <div
          role="alert"
          data-testid={`${id}-error`}
          className="mt-1 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700"
        >
          <span>
            {t('bankCatalog.loadError', 'Could not load the bank catalog: {{message}}', {
              message: errorMessage,
            })}
          </span>
          <button
            type="button"
            onClick={() => setAttempt((a) => a + 1)}
            className="ml-2 underline hover:no-underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-red-300"
          >
            {t('bankCatalog.retry', 'Retry')}
          </button>
          <div className="mt-1 text-xs text-red-600">
            {t(
              'bankCatalog.freeTextHint',
              'You can still type the bank name manually — it will be saved as free text.',
            )}
          </div>
        </div>
      )}

      {open && status === 'success' && (
        <ul
          id={listboxId}
          role="listbox"
          aria-label={t('bankCatalog.searchPlaceholder', 'e.g. 012 or BBVA')}
          data-testid={`${id}-listbox`}
          className="mt-1 max-h-56 overflow-y-auto rounded-md border border-slate-200 bg-white py-1 shadow-sm"
        >
          {filtered.map((bank, index) => (
            <li
              key={bank.id}
              id={`${baseId}-option-${index}`}
              role="option"
              aria-selected={selectedBank?.id === bank.id}
              data-testid={`${id}-option-${bank.code}`}
              onMouseDown={(e) => {
                // Mousedown precedes the deferred blur close, so selection lands first.
                e.preventDefault()
                commitSelection(bank)
              }}
              onMouseEnter={() => setActiveIndex(index)}
              className={cn(
                'cursor-pointer px-3 py-2 text-sm',
                index === activeIndex ? 'bg-slate-100' : 'bg-white',
              )}
            >
              {formatBankOption(bank)}
            </li>
          ))}
          {filtered.length === 0 && (
            <li className="px-3 py-2 text-sm text-slate-500" data-testid={`${id}-no-results`}>
              {query.trim()
                ? t(
                    'bankCatalog.useCustom',
                    'No catalog match — “{{name}}” will be saved as a manual bank.',
                    { name: query.trim() },
                  )
                : t('bankCatalog.noResults', 'No banks found.')}
            </li>
          )}
        </ul>
      )}
    </div>
  )
}
