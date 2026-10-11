import { useCallback, useEffect, useState } from 'react'
import { ApiError, apiClient } from '../../../api/client'

/**
 * Profile & Security mutations (module 15-user-profile, plan Phase 2).
 *
 * Mirrors `ProfileController` (`POST /api/v1/profile/...`) as serialized by
 * System.Text.Json (camelCase). The Bearer token travels automatically via the shared
 * `apiClient` interceptor; callers branch on `ApiError.code` - the spec's Domain Error
 * Codes (`AUTH_INVALID_PASSWORD`, ...) - and render them through `translateErrorCode`
 * against the `error` namespace (regenerated from the backend resx, never hand-edited).
 */

/** Wire-exact Domain Error Codes (spec §4) - the contract `translateErrorCode` resolves. */
export const ProfileErrorCodes = {
  InvalidPassword: 'AUTH_INVALID_PASSWORD',
  PasswordPolicyViolation: 'AUTH_PASSWORD_POLICY_VIOLATION',
  AccountLocked: 'AUTH_ACCOUNT_LOCKED',
  MfaInvalidCode: 'MFA_INVALID_CODE',
  MfaNotEnabled: 'MFA_NOT_ENABLED',
} as const

export interface Profile {
  email: string
  fullName: string
  twoFactorEnabled: boolean
}

export interface EnableMfaPayload {
  sharedKey: string
  authenticatorUri: string
  recoveryCodes: string[]
}

export interface ChangePasswordPayload {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

export async function fetchProfile(): Promise<Profile> {
  const response = await apiClient.get<Profile>('/v1/profile')
  return response.data
}

/** Resolves on 204; rejects with `ApiError` carrying the Domain Error Code. */
export async function changePassword(payload: ChangePasswordPayload): Promise<void> {
  await apiClient.post('/v1/profile/change-password', {
    currentPassword: payload.currentPassword,
    newPassword: payload.newPassword,
    confirmPassword: payload.confirmPassword,
  })
}

export async function enableMfa(): Promise<EnableMfaPayload> {
  const response = await apiClient.post<EnableMfaPayload>('/v1/profile/mfa/enable')
  return response.data
}

/** Resolves on 204 once the TOTP code finalizes enrollment. */
export async function verifyMfa(code: string): Promise<void> {
  await apiClient.post('/v1/profile/mfa/verify', { code })
}

export async function generateBackupCodes(): Promise<string[]> {
  const response = await apiClient.post<{ recoveryCodes: string[] }>('/v1/profile/mfa/backup-codes')
  return response.data.recoveryCodes
}

/** Resolves on 204 once possession is proven with a current TOTP code. */
export async function disableMfa(code: string): Promise<void> {
  await apiClient.post('/v1/profile/mfa/disable', { code })
}

export type MutationStatus = 'idle' | 'loading' | 'success' | 'error'

/**
 * Minimal mutation helper (the SPA ships no react-query): tracks status/error around one
 * async call so forms render Domain Error Codes without re-implementing try/catch.
 */
export function useApiMutation<TArgs extends unknown[], TResult>(
  fn: (...args: TArgs) => Promise<TResult>,
) {
  const [status, setStatus] = useState<MutationStatus>('idle')
  const [error, setError] = useState<ApiError | null>(null)

  const run = useCallback(
    async (...args: TArgs): Promise<TResult | null> => {
      setStatus('loading')
      setError(null)
      try {
        const result = await fn(...args)
        setStatus('success')
        return result
      } catch (cause: unknown) {
        setError(cause instanceof ApiError ? cause : new ApiError(0, 'Unexpected Error', String(cause)))
        setStatus('error')
        return null
      }
    },
    [fn],
  )

  const reset = useCallback(() => {
    setStatus('idle')
    setError(null)
  }, [])

  return { status, error, run, reset }
}

export function useProfile() {
  const [profile, setProfile] = useState<Profile | null>(null)
  const [status, setStatus] = useState<MutationStatus>('idle')
  const [error, setError] = useState<ApiError | null>(null)

  const reload = useCallback(async () => {
    setStatus('loading')
    setError(null)
    try {
      setProfile(await fetchProfile())
      setStatus('success')
    } catch (cause: unknown) {
      setError(cause instanceof ApiError ? cause : new ApiError(0, 'Unexpected Error', String(cause)))
      setStatus('error')
    }
  }, [])

  useEffect(() => {
    void reload()
  }, [reload])

  return { profile, status, error, reload }
}

export function useChangePassword() {
  return useApiMutation(
    useCallback((payload: ChangePasswordPayload) => changePassword(payload), []),
  )
}

export function useEnableMfa() {
  return useApiMutation(useCallback(() => enableMfa(), []))
}

export function useVerifyMfa() {
  return useApiMutation(useCallback((code: string) => verifyMfa(code), []))
}

export function useBackupCodes() {
  return useApiMutation(useCallback(() => generateBackupCodes(), []))
}

export function useDisableMfa() {
  return useApiMutation(useCallback((code: string) => disableMfa(code), []))
}
