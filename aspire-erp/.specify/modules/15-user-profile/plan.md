> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Plan: User Profile & Security

## Phase 1: Backend CQRS Setup & Domain Rules
1. Scaffold `ChangePasswordCommand`, `ChangePasswordCommandHandler` (implementing `ICommandHandler<ChangePasswordCommand, Result<Unit>>`), and validation (FluentValidation).
2. Scaffold `MfaCommands` (Enable, Verify, GenerateBackupCodes, Disable) with custom `ICommandHandler`.
3. Implement Domain Error Codes (`AUTH_INVALID_PASSWORD`, `MFA_INVALID_CODE`) in the Handlers.
4. Ensure ASP.NET Core Identity is configured for Lockout settings (e.g. 5 max failed access attempts).

## Phase 2: Frontend API Integration
1. Add `useProfile.ts` in `src/features/auth/api/` with mutation hooks for password and MFA endpoints.
2. Ensure network client correctly sends the Bearer token and parses Domain Error Codes to display localized error messages (e.g., via i18n).

## Phase 3: Frontend UI Components
1. Create `ProfileScreen.tsx` with a dual-tab layout (General, Security).
2. Create `ChangePasswordForm.tsx` (Current, New, Confirm inputs).
3. Create `MfaSetupModal.tsx` containing QR Code generator (e.g. `qrcode.react`), verification code input, and Backup Codes display step.
4. Hook up the dropdown in `Header.tsx` to navigate to `/profile`.
