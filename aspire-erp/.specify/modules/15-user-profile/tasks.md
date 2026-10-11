> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Tasks: User Profile & Security

- [ ] `Backend`: Create `ChangePasswordCommand.cs`, `ChangePasswordValidator.cs`, and `ChangePasswordCommandHandler.cs` in `Erp.Application/Features/Auth/`.
- [ ] `Backend`: Write Unit Tests for `ChangePasswordCommandHandler` covering success, wrong password, and lockout threshold.
- [ ] `Backend`: Create `MfaCommands.cs` and handlers (Enable, Verify, BackupCodes) ensuring old codes are invalidated upon generation.
- [ ] `Backend`: Add HTTP POST endpoints in `ProfileController.cs` for Password and MFA.
- [ ] `Frontend`: Create API hooks in `useProfile.ts` for Password and MFA mutations.
- [ ] `Frontend`: Map Backend `ErrorCodes` to i18n translation strings in `common.json`.
- [ ] `Frontend`: Build `ProfileScreen.tsx` layout and route.
- [ ] `Frontend`: Build `ChangePasswordForm.tsx` and integrate.
- [ ] `Frontend`: Build `MfaSetupModal.tsx` (must enforce user copying backup codes).
- [ ] `Frontend`: Update `Header.tsx` links to point to the new Profile screen.
