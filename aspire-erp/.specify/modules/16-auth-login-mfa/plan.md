> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Plan: Login MFA Challenge

## Phase 1: Backend CQRS & Login modification
1. Refactorizar el DTO de respuesta de `LoginCommandHandler` (ej. `LoginResultDto`) para agregar la propiedad booleana `IsMfaRequired`.
2. Interceptar el éxito de contraseña en `LoginCommandHandler` y chequear `user.TwoFactorEnabled`.
3. Crear el `VerifyLoginMfaCommand` y su respectivo `ICommandHandler<T>`.
4. Exponer un endpoint POST `v1/auth/login-mfa` en `AuthController`.

## Phase 2: Frontend API Integration
1. Ajustar el tipado del response de `POST /v1/auth/login` en el frontend para manejar el caso donde viene el JWT vs donde viene `IsMfaRequired: true`.
2. Agregar la llamada a `POST /v1/auth/login-mfa` en el API client.

## Phase 3: Frontend UI Components
1. Modificar `LoginScreen.tsx` para que, tras un login inicial exitoso donde `IsMfaRequired == true`, la UI reemplace el formulario de contraseña por un formulario de ingreso de código 2FA.
2. Permitir que el usuario pegue códigos TOTP (6 dígitos) o Backup Codes (8 dígitos) en el mismo input.
3. Mostrar mensajes de error limpios usando i18n para `AUTH_MFA_INVALID_CODE`.
