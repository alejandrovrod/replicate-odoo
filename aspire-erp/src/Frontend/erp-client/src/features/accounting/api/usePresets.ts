import { useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../../api/client'

/** One bundled industry preset (browser source). */
export interface VerticalPreset {
  key: string
  name: string
  description: string
  languages: string[]
}

/** What one application created vs skipped (admin report). */
export interface PresetApplyReport {
  presetKey: string
  customFieldsCreated: number
  customFieldsSkipped: number
  workflowsCreated: number
  workflowsSkipped: number
  taxTemplatesCreated: number
  taxTemplatesSkipped: number
}

export interface PresetsQuery {
  presets: VerticalPreset[]
  status: 'idle' | 'loading' | 'success' | 'error'
  error: ApiError | null
  reload: () => void
}

/** Every bundled industry preset (GET /api/v1/presets). */
export function usePresets(): PresetsQuery {
  const [attempt, setAttempt] = useState(0)
  const [presets, setPresets] = useState<VerticalPreset[]>([])
  const [status, setStatus] = useState<PresetsQuery['status']>('idle')
  const [error, setError] = useState<ApiError | null>(null)

  useEffect(() => {
    let cancelled = false
    setStatus('loading')
    setError(null)
    apiClient
      .get<VerticalPreset[]>('/v1/presets')
      .then(
        (response) => {
          if (!cancelled) {
            setPresets(response.data)
            setStatus('success')
          }
        },
        (cause: unknown) => {
          if (!cancelled) {
            setPresets([])
            setError(
              cause instanceof ApiError
                ? cause
                : new ApiError(0, 'Unexpected Error', cause instanceof Error ? cause.message : String(cause)),
            )
            setStatus('error')
          }
        },
      )
    return () => {
      cancelled = true
    }
  }, [attempt])

  return { presets, status, error, reload: () => setAttempt((c) => c + 1) }
}

/**
 * Applies one preset to a company (POST /api/v1/presets/apply). Idempotent:
 * re-applying skips existing rows and reports counts.
 */
export async function applyPreset(presetKey: string, companyId: string): Promise<PresetApplyReport> {
  const response = await apiClient.post<PresetApplyReport>('/v1/presets/apply', {
    presetKey,
    companyId,
  })
  return response.data
}
