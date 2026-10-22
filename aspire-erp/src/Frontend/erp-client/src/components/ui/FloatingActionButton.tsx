import type { ReactNode } from 'react'

interface FloatingActionButtonProps {
  label: string
  onClick: () => void
  disabled?: boolean
  icon?: ReactNode
  id?: string
  testId?: string
  ariaLabel?: string
}

/**
 * Standard list-view primary action: a bottom-right FAB so the table header stays focused on
 * title, search and filters. Mirrors the original MasterDataList pattern with the shared amber
 * palette used across the shell.
 */
export function FloatingActionButton({
  label,
  onClick,
  disabled = false,
  icon,
  id,
  testId,
  ariaLabel,
}: FloatingActionButtonProps) {
  return (
    <button
      type="button"
      id={id}
      data-testid={testId}
      aria-label={ariaLabel}
      onClick={onClick}
      disabled={disabled}
      className="fixed bottom-8 right-8 z-50 flex items-center gap-2 rounded-full bg-amber-600 px-5 py-3.5 text-sm font-semibold text-white shadow-lg shadow-amber-600/20 transition-all hover:-translate-y-0.5 hover:bg-amber-700 hover:shadow-xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-amber-600 focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50"
    >
      {icon ?? (
        <svg
          xmlns="http://www.w3.org/2000/svg"
          width="20"
          height="20"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true"
        >
          <path d="M5 12h14" />
          <path d="M12 5v14" />
        </svg>
      )}
      {label}
    </button>
  )
}
