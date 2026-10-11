> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Spec: Login MFA Challenge

## 1. Domain Overview
Este módulo complementa el módulo 15. Cubre la intercepción del flujo de Login tradicional para exigir un código TOTP (o código de recuperación) si la cuenta del usuario tiene el Multi-Factor Authentication habilitado. 

## 2. DDL & Schema Changes
- Ninguno. El esquema de `AspNetUsers` y `TwoFactorEnabled` ya fue gestionado.

## 3. Use Cases (CQRS) & Security Rules

### 3.1. Refactorización de `LoginCommand`
- **Cambio:** El `LoginCommandHandler` ya no asume que validar la contraseña es el único paso.
- **Regla:** Si las credenciales son correctas PERO `TwoFactorEnabled == true`, el comando NO debe emitir un JWT. En su lugar, debe devolver un objeto/resultado que indique `IsMfaRequired = true`, devolviendo solo el `Email` o un Token temporal cifrado si la arquitectura de Identity del proyecto lo requiere para el paso 2.

### 3.2. Nuevo Comando: `VerifyLoginMfaCommand`
- **Command:** `VerifyLoginMfaCommand`
  - `Email` o `TokenTemporal` (string, required)
  - `MfaCode` (string, required, length 6..8)
- **Regla:** Valida el código TOTP o el Backup Code contra el `UserManager`.
- **Seguridad (Lockout):** Cada intento fallido de MFA debe incrementar el `AccessFailedCount` para evitar fuerza bruta de los códigos de 6 dígitos. Si es exitoso, devuelve el JWT definitivo y resetea el Lockout.

## 4. Domain Error Codes
- `AUTH_MFA_REQUIRED`: Código 200/202 con payload indicando que falta el segundo factor.
- `AUTH_MFA_INVALID_CODE`: El código ingresado es incorrecto.
- `AUTH_ACCOUNT_LOCKED`: La cuenta se bloqueó por exceder el límite de intentos fallidos al meter el MFA.
