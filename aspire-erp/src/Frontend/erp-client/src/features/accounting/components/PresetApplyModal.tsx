import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  Dialog,
  DialogContent,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '../../../components/ui/Dialog'
import { Button } from '../../../components/ui/Button'
import { ApiError } from '../../../api/client'
import { translateErrorCode } from '../../../lib/translateErrorCode'
import { applyPreset, usePresets } from '../api/usePresets'
import { useTenantStore } from '../../../store/useTenantStore'

interface PresetApplyModalProps {
  isOpen: boolean
  onClose: () => void
}

/**
 * Industry preset browser (workflow Fase 5, gap D3): lists the bundled verticals with
 * what each seeds, applies one to the current company and reports created-vs-skipped
 * counts. Applying switches catalog rows AND screen vocabulary, so the page reloads
 * afterwards for the terminology overlay to take effect cleanly.
 */
export function PresetApplyModal({ isOpen, onClose }: PresetApplyModalProps) {
  const { t, i18n } = useTranslation('common')
  const companyId = useTenantStore((state) => state.companyId)
  const { presets, status } = usePresets()
  const [applyingKey, setApplyingKey] = useState<string | null>(null)
  const [report, setReport] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)

  const run = async (presetKey: string) => {
    setError(null)
    setReport(null)
    setApplyingKey(presetKey)
    try {
      const result = await applyPreset(presetKey, companyId)
      setReport(
        t('presets.applied', 'Applied {{name}}: {{fields}} custom fields, {{flows}} workflows, {{taxes}} tax templates ({{skipped}} rows already existed).', {
          name: presetKey,
          fields: result.customFieldsCreated,
          flows: result.workflowsCreated,
          taxes: result.taxTemplatesCreated,
          skipped:
            result.customFieldsSkipped + result.workflowsSkipped + result.taxTemplatesSkipped,
        }),
      )
      // Vocabulary + catalogs changed underneath the running app: reload for a clean slate.
      window.setTimeout(() => window.location.reload(), 1500)
    } catch (err: unknown) {
      if (err instanceof ApiError) {
        setError(translateErrorCode(i18n, err.code, err.message))
      } else if (err instanceof Error) {
        setError(err.message)
      } else {
        setError(t('presets.applyError', 'Could not apply the preset.'))
      }
    } finally {
      setApplyingKey(null)
    }
  }

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{t('presets.title', 'Industry presets')}</DialogTitle>
        </DialogHeader>

        <div className="space-y-3">
          <p className="text-xs text-slate-500">
            {t(
              'presets.subtitle',
              'One click seeds custom fields, approval flows, tax templates and screen vocabulary for a vertical. Re-applying never duplicates rows.',
            )}
          </p>
          {error && <div className="text-sm text-red-600">{error}</div>}
          {report && <div className="text-sm text-emerald-700">{report}</div>}

          {status === 'loading' ? (
            <p className="text-xs text-slate-500">{t('presets.loading', 'Loading presets…')}</p>
          ) : (
            <div className="space-y-2">
              {presets.map((preset) => (
                <div
                  key={preset.key}
                  className="flex items-center justify-between gap-3 rounded-lg border border-slate-200 px-3 py-2"
                >
                  <div>
                    <p className="text-sm font-semibold text-slate-900">{preset.name}</p>
                    <p className="text-xs text-slate-500">{preset.description}</p>
                  </div>
                  <Button
                    size="sm"
                    variant="outline"
                    disabled={applyingKey !== null}
                    onClick={() => void run(preset.key)}
                  >
                    {applyingKey === preset.key
                      ? t('presets.applying', '…')
                      : t('presets.apply', 'Apply')}
                  </Button>
                </div>
              ))}
            </div>
          )}
        </div>

        <DialogFooter className="pt-4 border-t border-slate-100">
          <Button type="button" variant="outline" onClick={onClose}>
            {t('action.close', 'Close')}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  )
}
