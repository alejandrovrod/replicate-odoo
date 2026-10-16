import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '../ui/Button'
import { ApiError } from '../../api/client'
import { translateErrorCode } from '../../lib/translateErrorCode'
import { applyWorkflowTransition, useWorkflowStatus } from './workflowApi'

interface WorkflowActionBarProps {
  documentType: string
  companyId: string
  documentId: string
  /** Refetch the host view after a successful move (lists show the new state). */
  onTransition?: () => void
}

/**
 * Operational action bar (workflow Fase 5, gap D5): the current workflow state badge
 * plus one button per executable transition. Buttons already arrive caller-filtered
 * (`allowedForCaller` is computed server-side from the JWT roles); the bar hides
 * itself when no flow governs the document.
 */
export function WorkflowActionBar({ documentType, companyId, documentId, onTransition }: WorkflowActionBarProps) {
  const { t, i18n } = useTranslation('common')
  const { status, hasFlow, loading } = useWorkflowStatus(documentType, companyId, documentId)
  const [actingTo, setActingTo] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  if (loading || !hasFlow || !status) return null

  const executable = status.transitions.filter((tr) => tr.allowedForCaller)
  if (executable.length === 0) {
    return (
      <span className="inline-flex items-center rounded-full bg-slate-100 px-2 py-0.5 text-[10px] font-semibold text-slate-700">
        {status.currentState}
      </span>
    )
  }

  const run = async (toState: string) => {
    setError(null)
    setActingTo(toState)
    try {
      await applyWorkflowTransition({ documentType, companyId, documentId, toState })
      onTransition?.()
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setError(translateErrorCode(i18n, err.code, err.message))
      } else if (err instanceof Error) {
        setError(err.message)
      } else {
        setError(t('workflow.transitionError', 'The transition failed.'))
      }
    } finally {
      setActingTo(null)
    }
  }

  return (
    <span className="inline-flex flex-wrap items-center gap-1">
      <span
        className="inline-flex items-center rounded-full bg-sky-100 px-2 py-0.5 text-[10px] font-semibold text-sky-800"
        title={t('workflow.currentState', 'Operational state')}
      >
        {status.currentState}
      </span>
      {executable.map((tr) => (
        <Button
          key={tr.toState}
          size="sm"
          variant="outline"
          disabled={actingTo !== null}
          onClick={() => void run(tr.toState)}
          title={
            tr.allowedRole
              ? t('workflow.requiresRole', 'Requires role {{role}}', { role: tr.allowedRole })
              : tr.actionLabel
          }
        >
          {actingTo === tr.toState ? t('workflow.working', '…') : tr.actionLabel}
        </Button>
      ))}
      {error && <span className="text-xs text-red-600">{error}</span>}
    </span>
  )
}
