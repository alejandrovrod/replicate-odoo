> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

# Tasks: Login MFA Challenge

- [x] `Backend`: Modificar `LoginResult` (o DTO equivalente) para soportar la bandera `IsMfaRequired`.
- [x] `Backend`: Actualizar `LoginCommandHandler.cs` para frenar la emisión del JWT si el MFA está prendido.
- [x] `Backend`: Crear `VerifyLoginMfaCommand.cs`, `VerifyLoginMfaValidator.cs`, y `VerifyLoginMfaCommandHandler.cs` aplicando los fallos al contador de Lockout.
- [x] `Backend`: Agregar endpoint `POST /v1/auth/login-mfa` en el `AuthController.cs` (o donde viva actualmente el login).
- [x] `Frontend`: Actualizar `LoginScreen.tsx` agregando un estado interno (`step === 'mfa'`) para mostrar el input de 6 dígitos en lugar del login tradicional.
- [x] `Frontend`: Conectar el envío del formulario de MFA hacia el nuevo endpoint `login-mfa`.
- [x] `Frontend`: Asegurar que el `useAuthStore.ts` invoque el `login(token, ...)` de Zustand solo cuando el endpoint de MFA devuelva éxito.
