# i18n Implementation Plan

## Backend Architecture

### 1. Localization Services Registration (`Erp.Api/Program.cs`)

```csharp
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en", "es" };
    options.SetDefaultCulture("en")
           .AddSupportedCultures(supportedCultures)
           .AddSupportedUICultures(supportedCultures);
    
    // Priority: user preference (future) > Accept-Language > default
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new QueryStringRequestCultureProvider(), // ?culture=es for testing
        new CookieRequestCultureProvider(),      // future: user preference cookie
        new AcceptLanguageHeaderRequestCultureProvider(),
    };
});
```

```csharp
app.UseRequestLocalization(); // Must be before UseAuthorization / MapControllers
```

### 2. Resource File Organization

```
src/Backend/Erp.Api/Resources/
├── Controllers/
│   ├── OpportunitiesController.en.resx
│   ├── OpportunitiesController.es.resx
│   ├── LeadsController.en.resx
│   └── ...
├── Shared/
│   ├── ErrorMessages.en.resx      // Keyed by error CODE (crm_loss_reason_required, etc.)
│   ├── ErrorMessages.es.resx
│   ├── ValidationMessages.en.resx // FluentValidation / DataAnnotations overrides
│   └── ValidationMessages.es.resx
└── Common/
    ├── Common.en.resx             // Shared UI strings (Save, Cancel, Delete, etc.)
    └── Common.es.resx
```

**Naming convention:** `{Module/Controller}.{culture}.resx` — matches ASP.NET Core's `IStringLocalizer<T>` lookup.

### 3. Error Message Catalog (`Shared/ErrorMessages.resx`)

Keys = **stable error codes** from domain (`CRMErrorCodes`, `SellingErrorCodes`, etc.)

| Key (Error.Code) | en Value | es Value |
|------------------|----------|----------|
| `crm_loss_reason_required` | "Loss reason is required when closing as lost." | "El motivo de pérdida es obligatorio al cerrar como perdida." |
| `crm_opportunity_not_found` | "Opportunity not found." | "Oportunidad no encontrada." |
| `invalid_status_transition` | "Invalid status transition." | "Transición de estado inválida." |
| `concurrency_conflict` | "The record was modified by another user. Please reload and try again." | "El registro fue modificado por otro usuario. Recargue e intente de nuevo." |

**Usage in controllers:**
```csharp
private readonly IStringLocalizer<ErrorMessages> _errors;

private ObjectResult OpportunityProblem(Error error) =>
    error.Code switch
    {
        "crm_opportunity_not_found" => Problem(404, "Opportunity Not Found", _errors[error.Code], error.Code),
        // ...
    };
```

### 4. FluentValidation / DataAnnotations Integration

**FluentValidation** (preferred for commands):
```csharp
public class AdvanceOpportunityStageCommandValidator : AbstractValidator<AdvanceOpportunityStageCommand>
{
    public AdvanceOpportunityStageCommandValidator(IStringLocalizer<ValidationMessages> v)
    {
        RuleFor(x => x.LossReason)
            .NotEmpty()
            .When(x => x.ToStage == "ClosedLost")
            .WithMessage(v["crm_loss_reason_required"]); // Key matches ErrorMessages key
    }
}
```

**DataAnnotations** (for DTOs):
```csharp
public class CreateSupplierCommand
{
    [Required(ErrorMessageResourceType = typeof(ValidationMessages), ErrorMessageResourceName = "supplier_name_required")]
    public string Name { get; set; }
}
```

### 5. ProblemDetails Mapping

The existing `Problem()` helper in controllers already injects `Extensions["code"] = error.Code`. We extend it to use localized title/detail:

```csharp
private ObjectResult Problem(int status, string titleKey, string detailKey, string? code)
{
    var title = _common[titleKey];      // e.g., "Opportunity Rejected" / "Oportunidad Rechazada"
    var detail = _errors[detailKey];    // e.g., localized message from ErrorMessages
    
    var problem = new ProblemDetails
    {
        Type = ...,
        Title = title,
        Status = status,
        Detail = detail,
        Instance = HttpContext.Request.Path.Value,
    };
    if (code != null) problem.Extensions["code"] = code;
    return new ObjectResult(problem) { StatusCode = status };
}
```

### 6. User Preference Persistence (Future)

When authentication lands:
- Add `LanguagePreference` column to `Users` table via **new migration**
- Implement `IRequestCultureProvider` that reads the authenticated user's preference
- Insert it at the top of `RequestCultureProviders` list

---

## Frontend Architecture

### 1. Package Dependencies

```json
{
  "dependencies": {
    "i18next": "^23.x",
    "react-i18next": "^14.x",
    "i18next-browser-languagedetector": "^7.x",
    "i18next-http-backend": "^2.x"
  }
}
```

### 2. i18next Configuration (`src/lib/i18n.ts`)

```typescript
import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import HttpBackend from 'i18next-http-backend';

i18n
  .use(HttpBackend)
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    fallbackLng: 'en',
    supportedLngs: ['en', 'es'],
    defaultNS: 'common',
    ns: ['common', 'error', 'crm', 'stock', 'selling', 'buying', 'manufacturing', 'banking', 'accounting', 'hr-payroll', 'assets'],
    
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage'],
      lookupLocalStorage: 'aspire-erp-lang',
    },
    
    backend: {
      loadPath: '/locales/{{lng}}/{{ns}}.json',
    },
    
    interpolation: { escapeValue: false },
    react: { useSuspense: false }, // We handle loading ourselves
  });

export default i18n;
```

### 3. Translation File Structure

```
src/Frontend/erp-client/public/locales/
├── en/
│   ├── common.json
│   ├── error.json
│   ├── crm.json
│   ├── stock.json
│   ├── selling.json
│   ├── buying.json
│   ├── manufacturing.json
│   ├── banking.json
│   ├── accounting.json
│   ├── hr-payroll.json
│   └── assets.json
└── es/
    ├── common.json
    ├── error.json
    ├── crm.json
    └── ... (same namespaces)
```

**Namespace per module** → enables lazy loading and code-splitting.

### 4. Type-Safe Translation Keys (Augmentation)

`src/types/i18n.d.ts`:
```typescript
import 'react-i18next';
import common from '../locales/en/common.json';
import error from '../locales/en/error.json';
import crm from '../locales/en/crm.json';
// ... import all en namespaces

declare module 'react-i18next' {
  interface Resources {
    common: typeof common;
    error: typeof error;
    crm: typeof crm;
    stock: typeof stock;
    selling: typeof selling;
    buying: typeof buying;
    manufacturing: typeof manufacturing;
    banking: typeof banking;
    accounting: typeof accounting;
    'hr-payroll': typeof hrPayroll;
    assets: typeof assets;
  }
}
```

**Effect:** `t('error.crm_loss_reason_required')` is fully typed. Missing key in `en` JSON → TypeScript error. Missing key in `es` JSON → runtime fallback to `en`.

### 5. Axios Interceptor for Accept-Language

Extend `src/api/client.ts`:
```typescript
apiClient.interceptors.request.use((config) => {
  // ... existing tenant/idempotency logic
  
  const lang = localStorage.getItem('aspire-erp-lang') || 'en';
  config.headers['Accept-Language'] = lang;
  
  return config;
});
```

### 6. Language Selector Component

`src/components/layout/LanguageSelector.tsx`:
```typescript
import { useTranslation } from 'react-i18next';
import { Globe } from 'lucide-react';

export function LanguageSelector() {
  const { i18n, t } = useTranslation();
  
  const changeLanguage = (lng: string) => {
    i18n.changeLanguage(lng);
    localStorage.setItem('aspire-erp-lang', lng);
  };
  
  return (
    <select value={i18n.language} onChange={(e) => changeLanguage(e.target.value)} className="...">
      <option value="en">English</option>
      <option value="es">Español</option>
    </select>
  );
}
```

Integrate into `Header.tsx` (right side, before notifications).

### 7. Integration with `useErpAction`

The hook already exposes `errorCode`. Components should use:
```tsx
const { state, dispatch } = useErpAction(advanceOpportunity);
const { t } = useTranslation('error');

return (
  <button onClick={() => dispatch({ toStage: 'ClosedLost' })}>
    Advance
  </button>
  {state.errorCode && (
    <Alert variant="destructive">
      {t(`error.${state.errorCode}`)}
    </Alert>
  )}
);
```

**Migration path:** Existing components using `state.error` (raw message) can be migrated one by one. The `error` namespace in `en.json` mirrors current English messages for seamless transition.

### 8. App Bootstrap

`src/main.tsx`:
```typescript
import './lib/i18n'; // Initialize i18next before rendering
// ... rest unchanged
```

---

## Module Pilot: CRM

The CRM module is the pilot because:
- Has existing error codes (`CRMErrorCodes`)
- Has controllers with ProblemDetails mapping (`OpportunitiesController`, `LeadsController`)
- Frontend has `CrmOverview`, `OpportunityKanbanBoard`, `OpportunityCard`
- Error codes surfaced via `useErpAction` in `CreateSalesOrderForm`, `OpportunityCard`

**Pilot scope:**
1. Backend: `ErrorMessages.resx` with CRM keys, controller refactor to use `IStringLocalizer`
2. Frontend: `error.json` (en/es) with CRM keys, `crm.json` for UI labels, language selector
3. Verification: Integration test for `Accept-Language: es` → localized ProblemDetails

---

## Testing Strategy

### Backend Tests

| Test | Description |
|------|-------------|
| `ErrorLocalization_Es_ReturnsSpanishProblemDetails` | POST to `/api/v1/opportunities/{id}/advance` with `Accept-Language: es` and missing `LossReason` → 400 with Spanish `title`/`detail`, invariant `code` |
| `ErrorLocalization_En_ReturnsEnglishProblemDetails` | Same request, `Accept-Language: en` → English |
| `ErrorLocalization_Fallback_UnknownLanguage` | `Accept-Language: fr` → falls back to English |
| `ValidationLocalization_Es_ReturnsSpanishMessages` | FluentValidation/DataAnnotations on command with Spanish header |

### Frontend Tests

| Test | Description |
|------|-------------|
| `LanguageSelector_ChangesLanguageAndPersists` | Render selector, click Español → UI re-renders in Spanish, localStorage updated |
| `ErrorTranslation_ShowsSpanishForKnownCode` | Simulate `ApiError` with `code: 'crm_loss_reason_required'` → displays Spanish message |
| `ErrorTranslation_FallbackToEnglishForMissingKey` | Simulate `ApiError` with code only in `en.json` → displays English message |
| `TypeScriptCompile_FailsOnMissingEnKey` | (CI check) Adding key to `es.json` without `en.json` → build fails |

---

## Rollout Order

1. **Infrastructure** (both sides): packages, config, resource file scaffolding, language selector
2. **Shared error catalog**: `ErrorMessages.resx` + `error.json` with all existing error codes
3. **CRM pilot**: Controller refactor, CRM namespace, component migration
4. **Module by module**: Stock, Selling, Buying, Manufacturing, Banking, Accounting, HR/Payroll, Assets
5. **Validation messages**: FluentValidation/DataAnnotations localization
6. **User preference persistence** (backend): when auth lands