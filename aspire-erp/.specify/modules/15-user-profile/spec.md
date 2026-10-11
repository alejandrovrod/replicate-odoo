> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Spec: User Profile & Security (MFA, Password)

## 1. Domain Overview
This module governs user profile settings, password changes, and Multi-Factor Authentication (MFA) capabilities (TOTP + Backup Codes) for `aspire-erp`. It implements security using ASP.NET Core Identity combined with the project's custom `ICommandHandler<T>` CQRS pattern (returning `Result<T>`).

## 2. DDL & Schema Changes
- `AspNetUsers` (existing): Leverage `TwoFactorEnabled` and `AccessFailedCount` for brute-force lockouts.
- `AspNetUserTokens` (existing): Store `RecoveryCodes` and `AuthenticatorKey`. All sensitive tokens MUST be handled by ASP.NET Core Identity's built-in token providers which hash them before persistence.

## 3. Use Cases (CQRS) & Security Rules
### 3.1. Change Password
- **Command:** `ChangePasswordCommand`
  - `CurrentPassword` (string, required)
  - `NewPassword` (string, required, MinLength 12)
  - `ConfirmPassword` (string, required, Compare NewPassword)
- **Rules:** Must validate current password. If valid, update and return `Result<Unit>.Success()`. If invalid, increment `AccessFailedCount` to protect against brute-force.

### 3.2. MFA (Multi-Factor Authentication)
- **Command:** `EnableMfaCommand`
  - Generates TOTP URI (for QR code) and initial recovery codes.
- **Command:** `VerifyMfaCommand`
  - Verifies the TOTP code to finalize MFA enablement. Replay attacks are prevented by Identity tracking the last used timestamp/code.
- **Command:** `GenerateBackupCodesCommand`
  - Overwrites existing recovery codes with 10 new generated codes. Invalidates any previously issued recovery codes.

## 4. Domain Error Codes
- `AUTH_INVALID_PASSWORD`: Current password does not match.
- `AUTH_PASSWORD_POLICY_VIOLATION`: New password does not meet complexity rules.
- `AUTH_ACCOUNT_LOCKED`: User is temporarily locked out due to too many failed attempts.
- `MFA_INVALID_CODE`: The provided TOTP code is incorrect or expired.
- `MFA_NOT_ENABLED`: Attempted to generate backup codes when MFA is off.

## 5. UI/UX
- **Profile Screen:** Accessible via the Header dropdown.
- **Security Tab:** Password change form and MFA toggle.
- **MFA Flow:** QR code display for TOTP apps, input for 6-digit code, and mandatory backup codes download step before finalizing.
