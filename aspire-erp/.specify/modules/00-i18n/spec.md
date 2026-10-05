# i18n Specification

## Scope

This specification defines the internationalization (i18n) infrastructure for the Aspire ERP system, covering both the ASP.NET Core backend and React frontend. The goal is to enable a fully localized user experience for English and Spanish while maintaining the architectural invariant that **domain data (enum values, error codes, DB-stored strings) never leaves the backend in English**.

**In scope:**
- Backend: localized ProblemDetails responses, FluentValidation/DataAnnotations messages, shared error message catalog
- Frontend: i18next integration, namespace-based translation files, language selector, HTTP Accept-Language header injection
- Language resolution strategy and persistence
- Fallback behavior and developer experience (type-safe translation keys)

**Out of scope:**
- Right-to-left (RTL) language support
- Currency/number/date formatting beyond Intl/CultureInfo usage (already covered by existing code)
- Translation of persisted domain data (OpportunityStatus.Open, error codes, etc.)

---

## Supported Languages

| Code | Name | Native Name | Status |
|------|------|-------------|--------|
| `en` | English | English | Default / Fallback |
| `es` | Spanish | Español | Primary target |

Additional languages may be added later by dropping resource files without code changes.

---

## Language Resolution Strategy

The resolved UI language is determined by the following priority order (highest first):

1. **User preference** — explicitly stored choice (localStorage on frontend, user profile column on backend once auth lands)
2. **`Accept-Language` header** — sent by browser / API client
3. **Default** — `en`

### Frontend Resolution Flow

```
on app bootstrap:
  1. read localStorage('aspire-erp-lang') → if valid (en|es) use it
  2. else read navigator.language / navigator.languages → match first supported
  3. else 'en'
```

The resolved language is:
- Used to initialize `i18next`
- Persisted to localStorage on explicit user change
- Sent as `Accept-Language` header on every API request via axios interceptor

### Backend Resolution Flow

```
on each request:
  1. if authenticated user has LanguagePreference → use it
  2. else parse Accept-Language header (RFC 7231) → match first supported
  3. else 'en'
```

The resolved culture is set via `UseRequestLocalization` and flows into:
- `ProblemDetails.Title` / `Detail` localization
- FluentValidation / DataAnnotations validation messages
- Any backend-generated user-facing strings

---

## User Preference Storage

### Frontend (Immediate)
- **Key:** `aspire-erp-lang` in localStorage
- **Value:** `en` or `es`
- **Migration:** If missing or invalid, falls back to resolution strategy above

### Backend (Future - when auth lands)
- **Table:** `Users` (or `UserProfiles`)
- **Column:** `LanguagePreference` (char(2), nullable)
- **Migration:** New EF Core migration (not modifying existing ones)
- **Default:** NULL → falls back to Accept-Language → `en`

---

## Fallback Rules

1. **Missing key in target language** → fall back to `en` **silently** (no console error, no UI break)
2. **Missing key in `en`** → this is a **build-time error** (TypeScript augmentation catches it; backend resx compilation catches it)
3. **Unsupported language requested** → treat as missing → fall back to `en`
4. **Backend localization failure** → log warning, return `en` strings, never 500

---

## Acceptance Criteria

### AC-01: Language Selector
- A language selector (dropdown: English / Español) is visible in the app shell (Header)
- Changing it immediately re-renders the UI in the new language
- Selection persists across reloads (localStorage)

### AC-02: Backend Error Localization
- Given `Accept-Language: es`, a 400 response for `crm_loss_reason_required` returns:
  ```json
  {
    "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
    "title": "Oportunidad Rechazada",
    "status": 400,
    "detail": "El motivo de pérdida es obligatorio al cerrar como perdida.",
    "instance": "/api/v1/opportunities/.../advance",
    "code": "crm_loss_reason_required"
  }
  ```
- Given `Accept-Language: en` (or missing), the same error returns English title/detail
- The `code` field is **always** the invariant English machine code

### AC-03: Frontend Error Translation
- `useErpAction` receives `ApiError` with `error.code`
- UI displays `t(`error.${error.code}`)` — the translated message from the `error` namespace
- If key missing in `es`, falls back to `en` translation
- If key missing in `en`, TypeScript compilation fails

### AC-04: Validation Messages
- FluentValidation rules on commands return localized messages per `Accept-Language`
- DataAnnotations on DTOs return localized messages per `Accept-Language`

### AC-05: Type-Safe Translation Keys
- TypeScript: `declare module 'react-i18next' { interface Resources { common: {...}; error: {...}; crm: {...}; } }`
- Adding a key to `en` JSON without adding to `es` → TypeScript error (if strict) or runtime fallback
- Backend: `.resx` files — missing key in `es` falls back to `en` at runtime; missing in `en` = compile error

### AC-06: Regional Formatting
- Dates: `Intl.DateTimeFormat` (frontend) / `CultureInfo` (backend) — no manual formatting
- Numbers: `Intl.NumberFormat` / `CultureInfo`
- Currency: `Intl.NumberFormat` with `style: 'currency'` / `CultureInfo`

### AC-07: No Hardcoded Strings
- Zero user-visible strings in React components (except component-internal keys like `data-testid`)
- Zero user-visible strings in backend handlers/controllers (all via `IStringLocalizer` or validation attributes)

---

## Non-Goals / Explicitly Deferred

- Server-side rendering (SSR) language detection
- Per-module lazy loading of backend resources (single shared resx per module is sufficient)
- Translation management UI / CMS integration
- Pluralization rules beyond i18next's built-in support