import { apiClient } from '../../../api/client'

/**
 * Login + MFA challenge calls (module 16-auth-login-mfa, plan Phase 2).
 *
 * `POST /v1/auth/login` answers with exactly one populated shape (mirrors
 * `LoginResponseDto` serialized camelCase):
 * - password-only accounts: `{ token, fullName, email, isMfaRequired: false }`;
 * - MFA accounts: `{ token: null, fullName, email, isMfaRequired: true, mfaTicket }`.
 * The ticket is a short-lived password proof - `POST /v1/auth/login-mfa` redeems it with
 * a 6-digit TOTP code or an 8-char backup code (dashes accepted, stripped server-side)
 * and answers with the definitive JWT payload.
 */

export interface LoginResponse {
  token: string | null
  fullName: string
  email: string
  isMfaRequired: boolean
  mfaTicket: string | null
}

export async function loginRequest(email: string, password: string): Promise<LoginResponse> {
  const response = await apiClient.post<LoginResponse>('/v1/auth/login', { email, password })
  return response.data
}

export async function verifyLoginMfa(mfaTicket: string, mfaCode: string): Promise<LoginResponse> {
  const response = await apiClient.post<LoginResponse>('/v1/auth/login-mfa', { mfaTicket, mfaCode })
  return response.data
}
