# i18n Implementation Tasks

## Phase 0 — Infrastructure (Backend)

- [x] **B-01** Add NuGet packages: `Microsoft.Extensions.Localization`, `Microsoft.AspNetCore.Localization` to `Erp.Api`
- [x] **B-02** Create `Resources/` folder structure in `Erp.Api`:
  - `Resources/Shared/ErrorMessages.en.resx`
  - `Resources/Shared/ErrorMessages.es.resx`
  - `Resources/Shared/ValidationMessages.en.resx`
  - `Resources/Shared/ValidationMessages.es.resx`
  - `Resources/Common/Common.en.resx`
  - `Resources/Common/Common.es.resx`
- [x] **B-03** Register localization in `Program.cs`:
  - `AddLocalization(options => options.ResourcesPath = "Resources")`
  - `Configure<RequestLocalizationOptions>` with `en`/`es`, default `en`, providers: QueryString → Cookie → AcceptLanguage
  - `app.UseRequestLocalization()` before `UseAuthorization`
- [x] **B-04** Create `IStringLocalizer<ErrorMessages>` and `IStringLocalizer<Common>` injection in controllers
- [x] **B-05** Populate `ErrorMessages.en.resx` with **all existing error codes** from domain:
  - `CRMErrorCodes` (24 codes)
  - `SellingErrorCodes`
  - `PurchaseErrorCodes`
  - `StockErrorCodes`
  - `ManufacturingErrorCodes`
  - `JournalErrorCodes`
  - `HrPayrollErrorCodes`
  - `BankingErrorCodes`
  - `AssetErrorCodes`
  - `AccountingErrorCodes`
  - `AccountErrorCodes`
  - `ConcurrencyErrorCodes`
  - `FinancialReportErrorCodes`
- [x] **B-06** Populate `ErrorMessages.es.resx` with Spanish translations for all above codes
- [x] **B-07** Populate `Common.en.resx` / `Common.es.resx` with shared UI titles:
  - `OpportunityRejected`, `OpportunityNotFound`, `OpportunityConflict`, `InvalidRequest`, `ServerError`, `NotFound`, `Conflict`, `BadRequest`
- [x] **B-08** Refactor `OpportunitiesController.OpportunityProblem()` to use localized strings:
  - `_errors[error.Code]` for detail
  - `_common["OpportunityNotFound"]` etc. for title
- [x] **B-09** Refactor `LeadsController` to use localized ProblemDetails (same pattern)
- [x] **B-10** Add integration test: `ErrorLocalization_Es_ReturnsSpanishProblemDetails` in `Erp.Api.IntegrationTests`
- [x] **B-11** Add integration test: `ErrorLocalization_En_ReturnsEnglishProblemDetails`
- [x] **B-12** Add integration test: `ErrorLocalization_Fallback_UnknownLanguage`
- [x] **B-13** Run full test suite → all green

## Phase 0 — Infrastructure (Frontend)

- [x] **F-01** Install packages: `i18next`, `react-i18next`, `i18next-browser-languagedetector`, `i18next-http-backend`
- [x] **F-02** Create `src/lib/i18n.ts` with configuration (fallback `en`, supported `en`/`es`, namespaces, detector, HTTP backend)
- [x] **F-03** Create `public/locales/en/` with namespace files:
  - `common.json` (Save, Cancel, Delete, Search, Loading, Error, Success, etc.)
  - `error.json` — **mirror all backend error codes** with current English messages
  - `crm.json` (Pipeline, Lead, Opportunity, Stage, Probability, LossReason, Convert, Advance, Reopen, etc.)
  - `stock.json`, `selling.json`, `buying.json`, `manufacturing.json`, `banking.json`, `accounting.json`, `hr-payroll.json`, `assets.json` (minimal keys for now)
- [x] **F-04** Create `public/locales/es/` with Spanish translations for all above
- [x] **F-05** Create `src/types/i18n.d.ts` with module augmentation for type-safe keys (import all `en` JSON files)
- [x] **F-06** Import `./lib/i18n` in `src/main.tsx` (before App render)
- [x] **F-07** Add `Accept-Language` header interceptor in `src/api/client.ts` (read from localStorage `aspire-erp-lang`)
- [x] **F-08** Create `src/components/layout/LanguageSelector.tsx` component
- [x] **F-09** Integrate `LanguageSelector` into `Header.tsx` (right side, before notifications)
- [x] **F-10** Verify: app loads in English, switch to Spanish → UI re-renders, localStorage persists, reload → Spanish
- [x] **F-11** Run `npm run build` → TypeScript compiles (no missing key errors)
- [x] **F-12** Run `npm run lint` → clean

## Phase 1 — CRM Pilot (Backend)

- [x] **B-14** Refactor `OpportunitiesController`:
  - Inject `IStringLocalizer<ErrorMessages> _errors`, `IStringLocalizer<Common> _common`
  - Replace hardcoded titles/detail with localized lookups
  - Keep `error.Code` invariant in `Extensions["code"]`
- [x] **B-15** Refactor `LeadsController` (same)
- [x] **B-16** Add FluentValidation localizers to CRM validators → **done in substance, premise corrected**: the solution has no FluentValidation; domain validators (`LeadValidator`, `OpportunityValidator`, `*CommandHandler`) raise `CRMValidationException(code, english)` and CRM controllers localize via `_errors.Text(error.Code, error.Message)`. Catalog completed (11 previously-unlisted codes added) + test below.
- [x] **B-17** Run CRM-related integration tests → green

## Phase 1 — CRM Pilot (Frontend)

- [x] **F-13** Update `CrmOverview.tsx` to use `useTranslation('crm')` for all labels
- [x] **F-14** Update `OpportunityKanbanBoard.tsx` / `OpportunityCard.tsx` to use `t('crm.*')` keys
- [x] **F-15** Update `CreateSalesOrderForm.tsx` to use `useTranslation('crm')` + `useTranslation('error')`
- [x] **F-16** In components using `useErpAction`, replace `state.error` display with `t(`error.${state.errorCode}`)` from `error` namespace
- [x] **F-17** Verify: trigger CRM errors (e.g., advance to ClosedLost without LossReason) → Spanish error shows when language=Es
- [x] **F-18** Run `npm run build` → clean
- [x] **F-19** Run frontend tests (if any) → green

## Phase 2 — Module Rollout (Repeat per module)

**Deviation (applies to every module):** no per-controller resx files. Controller-level titles
go into the shared `Resources/Common/CommonMessages{,.es}.resx` (`{Resource}NotFound`,
`{Resource}Conflict`, `{Resource}Rejected`, plus genuinely shared titles like
`FiscalPeriodLocked`) — one source of truth per layer, no `Controllers/` sprawl. Details always
resolve through the shared `ErrorMessages` catalog via `_errors.Text(error.Code, error.Message)`.
`FluentValidation` items are n/a everywhere (see Phase 0 deviation).

**Instance-valued details pass through (proven twice, do not "fix"):** when a domain message
embeds runtime values asserted by tests — `fiscal_period_locked` (posting date + period
boundary, `FiscalPeriodLockApiTests`), `duplicate_customer_code` (colliding code,
`CustomersApiTests`) — the controller returns `error.Message` untranslated while the title
still localizes. A static catalog string can never carry those values; the long-term answer is
format args on the `Error` record (touches every handler — explicitly out of Phase 2 scope).
409-duplicate titles stay specific when a test pins the wire wording (`DuplicateCustomerCode`);
otherwise the generic `{Resource}Conflict` applies.

### Stock ✅ (done)

- [x] **B-S01** Refactored `StockEntriesController`, `WarehousesController`, `ItemsController` to
  `_errors.Text` / `_common.Text` (+8 title keys in `CommonMessages`, split combined 409 arm so
  `FiscalPeriodLocked` keeps its own title)
- [x] **B-S02** Localization test `StockEntriesList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails` (zero-write guard)
- [x] **F-S01** `stock.json` (en/es) rewritten with real UI keys: overview/stats/warehouses/items/movements/entry/entryType/validation; `format.ts` follows UI language via `Intl` (`displayLocale()`)
- [x] **F-S02** Migrated `StockOverview`, `ItemList`, `StockEntryModal` to `useTranslation('stock')`; server errors via `translateErrorCode(i18n, code, fallback)`; client validation translated at submit (transient), server errors at render (reactive); entry-type labels via typed `t(\`entryType.${entryType}\`)`
- [x] **F-S03** `npm run build` + `npm run lint` clean

### Selling ✅ (done)

- [x] **B-G01** Refactored `CustomersController`, `SalesOrdersController`, `DeliveryNotesController`,
  `SalesInvoicesController` to `_errors.Text` / `_common.Text` (+14 title keys in `CommonMessages`,
  incl. shared `ConcurrentUpdateConflict`; nested 409 title switch preserved; `ToActionResult`
  `rejectedTitle` param dropped as dead)
- [x] **B-G02** `SalesInvoicesController.SubmitPOSInvoice` converted from `BadRequest(new { Error })`
  (unreadable by the shared `apiClient` interceptor — its message never reached the UI) to RFC 7807
  with localized title/detail; same 400 status, `SalesInvoiceRejected` title
- [x] **B-G03** Localization test `CustomersList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails`
  (zero-write guard)
- [x] **F-G01** `selling.json` (en/es) with real UI keys; `src/lib/format.ts` created (locale-aware
  `displayLocale`/`formatMoney`/`formatQty`), `features/stock/format.ts` now re-exports it;
  `wire` values (`Cash`/`Card`, entry types, demo fixture rows) untouched
- [x] **F-G02** Migrated `SellingOverview`, `PosCashierModal` to `useTranslation('selling')`; POS error
  via `translateErrorCode`; money via shared `formatMoney`
- [x] **F-G03** `npm run build` + `npm run lint` clean; targeted suite 25/25 green (incl. the pinned
  duplicate-customer and delivery-note tests)

### Buying ✅ (done)

- [x] **B-B01** Refactored `SuppliersController`, `PurchaseOrdersController`,
  `PurchaseInvoicesController`, `PurchaseReceiptsController` to `_errors.Text` / `_common.Text`
  (+17 title keys in `CommonMessages`; nested 409 title switches preserved; `Update` split into
  per-code arms — `PurchaseOrderNotFound`/`CompanyNotFound`→`InvalidCompany`/`SupplierNotFound`,
  `InvalidStatusTransition` keeps pinned `"Conflict"` per `PurchaseOrdersApiTests`,
  `ConcurrencyConflict`→shared `ConcurrentUpdateConflict`)
- [x] **B-B02** Route/payload mismatch guards (previously framework `Problem` without `code`) now go
  through the private `Problem` with new catalog codes `route_id_mismatch`/`route_company_mismatch`
  (static, fully localizable, in shared `ErrorMessages` vocabulary)
- [x] **B-B03** Passthrough details (Phase 2 convention): `OverbillingNotAllowed` (quantities, pinned),
  `InvoiceAlreadyExists` (bill number), `DuplicateSupplierCode` (code), `FiscalPeriodLocked`
  (dates); specific `Duplicate{SupplierCode,PurchaseInvoice}` titles
- [x] **B-B04** Localization test `PurchaseOrdersList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails`
- [x] **F-B01** `buying.json` (en/es) with real UI keys; `src/lib/format.ts` created (locale-aware
  money/qty), `features/stock/format.ts` re-exports it; `BuyingOverview.formatCurrency` replaced;
  wire states (`Draft`/`Submitted`/…, `Cash`/`Card`-style values) and demo fixture rows untouched
- [x] **F-B02** Migrated `BuyingOverview` (incl. `catch (err: any)` → `unknown` + `ApiError` narrowing),
  `PurchaseReceiptModal` to `useTranslation('buying')`; receipt error via `translateErrorCode`;
  `{error && …}` with `unknown` does not compile — ternary like `CrmOverview`
- [x] **F-B03** `npm run build` + `npm run lint` clean; targeted suite 23/23 green (incl. pinned
  overbilling details, 409-after-submit, purchase-invoice tests)

### Manufacturing ✅ (done)

- [x] **B-M01** Refactored `BomsController`, `WorkOrdersController` to `_errors.Text` / `_common.Text`
  (+9 title keys in `CommonMessages`: `InvalidBom`/`BomNotFound`, `InvalidWorkOrder`,
  `WorkOrder{NotFound,Conflict,Rejected}`, `Manufacturing{ResourceNotFound,Conflict,Rejected}`;
  repeated guards/switch arms via `replaceAll`; `ManufacturingProblem` 409 group keeps one arm with
  nested detail switch for `FiscalPeriodLocked` passthrough)
- [x] **B-M02** Localization test `BomsList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails`
- [x] **F-M01** `manufacturing.json` (en/es) with real UI keys (overview/cards/board/actions/complete/bom);
  `BomEditor.money()` (hardcoded `en-US` + `$`) replaced by shared `formatMoney`; status badges stay
  raw wire values; `Dr`/`Cr` amounts via `formatMoney`
- [x] **F-M02** Migrated `ManufacturingOverview` (raw-error state pattern), `WorkOrdersBoard`
  (4 `useErpAction` errors merged via `translateErrorCode`, action labels/titles, interpolated
  progress line), `BomEditor` to `useTranslation('manufacturing')`
- [x] **F-M03** `npm run build` + `npm run lint` clean; targeted suite 18/18 green (manufacturing tests
  assert codes/statuses only — no title/detail pins)

### Banking ✅ (done)

- [x] **B-K01** Refactored `BankTransactionsController`, `BankStatementImportsController`,
  `BankTransactionRulesController` to `_errors.Text` / `_common.Text` (+10 title keys in
  `CommonMessages`: `BankTransactionNotFound`, `RuleRunRejected`, `UnreconcileRejected`,
  `QuickVoucher{CounterpartNotFound,Rejected}`, `Reconciliation{CounterpartNotFound,Rejected}`,
  `BankAccountNotFound`, `StatementImportRejected`, `RuleRejected`; `FiscalPeriodLocked` detail
  passthrough in the quick-voucher arm)
- [x] **B-K02** Localization test `BankRulesList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails`
- [x] **F-K01** `banking.json` (en/es) with real UI keys incl. `rulesMatched_one/_other` plurals;
  bank amounts via shared `formatQty` + currency code (not `formatMoney` — multi-currency lines,
  currency is data); `Dr`/`Cr` → `Debe`/`Haber` in es voucher detail; statuses/filter values stay
  raw wire values; demo placeholders translated (`e.g. 5150` → `p. ej. 5150`)
- [x] **F-K02** Migrated `BankingOverview` (raw-error state), `BankReconciliation` (merged
  reconcile/unreconcile errors via one `translateErrorCode`, ternary for `unknown && JSX`),
  `BankStatementImporter` (client file-read flag → boolean + `t('importer.readFailed')`),
  `VoucherQuickCreateDialog` to `useTranslation('banking')`
- [x] **F-K03** `npm run build` + `npm run lint` clean; targeted suite 18/18 green (statement-import
  tests assert data fields only — no title pins)

### Accounting ✅ (done)

- [x] **B-C01** Refactored `AccountsController`, `JournalEntriesController`,
  `FinancialReportsController` to `_errors.Text` / `_common.Text` (+11 title keys in
  `CommonMessages`: `DuplicateAccountCode`, `AccountValidationFailed`, `InvalidJournalEntry`,
  `JournalEntry{NotFound,Conflict,Rejected}`, `InvalidDateRange`, `MissingPeriod`,
  `InvalidPeriod`, `MissingDate`, `InvalidDate`; dead `actionName` param dropped from
  `ToActionResult`; route/payload guards already had codes)
- [x] **B-C02** `ValidatePeriod` no longer interpolates the parameter name (dead `parameterName`
  removed): details come from the catalog, the title names the failure mode — the param already
  travels in the request URL
- [x] **B-C03** Localization test `GeneralLedgerReport_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails`
- [x] **F-C01** `accounting.json` (en/es) with real UI keys (missingTenant/tree/ledger); shared
  `formatDelta` moved to `src/lib/format.ts` next to `formatMoney`; root-type badges stay raw wire
  enums; `&Sigma;` entities replaced by literal Σ in the catalog string
- [x] **F-C02** Migrated `AccountTreeTable` (filter/aria/headers/empty/active + `translateErrorCode`
  for the message, code badge kept), `GeneralLedgerOverview` to `useTranslation('accounting')`
- [x] **F-C03** `npm run build` + `npm run lint` clean; targeted suite 29/29 green — incl. ALL pinned
  journal titles (`Journal Entry Rejected/Conflict`, `Fiscal Period Locked`,
  `Concurrent Update Conflict`), pinned date-range titles, fiscal date details, and 11/11
  localization tests

### HR/Payroll ✅ (done)

- [x] **B-H01** Refactored `HrPayrollMastersController`, `PayrollEntriesController` to
  `_errors.Text` / `_common.Text` (+6 title keys in `CommonMessages`:
  `InvalidPayroll{Disbursement,Entry}`, `Payroll{EntryNotFound,ResourceNotFound,Conflict,Rejected}`;
  `PayrollProblem` 409 group keeps one arm with nested detail switch for
  `PayrollPeriodOverlap`/`DuplicateSalarySlip`/`FiscalPeriodLocked` passthroughs; masters guards
  use `company_not_found` (not `company_required`) — caught by the new test asserting the wrong
  message, fixed to `"Empresa no encontrada."`)
- [x] **B-H02** Localization test `EmployeesList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails`
- [x] **F-H01** `hr-payroll.json` (en/es) with real UI keys incl. `summary_one/_other` and
  `submitted_one/_other` plurals; statuses/badges stay raw wire values
- [x] **F-H02** Migrated `EmployeeDirectory` (eligibility badges + tooltips), `HrPayrollOverview`
  (raw-error state), `PayrollWorkbench` (merged 3-action errors via one `translateErrorCode`,
  interpolated submit confirmation with `voucherSuffix`, composed progress/started/ended line,
  `formatMoney` for pay amounts) to `useTranslation('hr-payroll')`
- [x] **F-H03** `npm run build` + `npm run lint` clean; targeted suite 19/19 green (payroll lifecycle
  asserts data/statuses only — no title pins)

### Assets ✅ (done — backend only, no screens exist)

- [x] **B-A01** Refactored `AssetsController`, `AssetCategoriesController` to `_errors.Text` /
  `_common.Text` (+6 title keys in `CommonMessages`: `InvalidAsset`, `AssetNotFound`,
  `AssetResourceNotFound`, `AssetConflict`, `AssetRejected`, `AssetCategoryRejected`; detail
  404 keeps interpolated ids as passthrough; `FiscalPeriodLocked` detail passthrough in
  `AssetProblem`)
- [x] **B-A02** Localization test `AssetsList_EmptyCompanyId_WithEsHeader_ReturnsSpanishProblemDetails`
- [x] **F-A01** n/a — no asset screens exist; `assets.json` stub (module title/subtitle) suffices
- [x] **F-A02** Full suite: `dotnet test Erp.sln` 827 green (242+489+96); `i18n-sync --check` OK;
  `npm run build` + `npm run lint` clean

### Beyond spec (needed for full coverage)

- [x] **X-01** `Header.tsx` translated (`nav.route.*`, `header.*`, `state.connected`) — no task owned it
- [x] **X-02** `Sidebar.tsx` translated (`nav.group.*`, `nav.item.*`, expand/collapse) — brand strings
  ("Aspire ERP", "ERPNext" badges, stack footer) intentionally literal as product identity
- [x] **X-03** `DashboardOverview.tsx` translated via new `dashboard` namespace (12th entry in
  `NAMESPACES`); KPI values stay literal mock data until a real endpoint exists
- [x] **X-04** `App.tsx` accounting-coa page wrapper translated (`accounting.page.*`)

For each remaining module: **Selling, Buying, Manufacturing, Banking, Accounting, HR/Payroll, Assets**

> Superseded: every module below shipped with its own subsection (Stock ✅, Selling ✅, Buying ✅,
> Manufacturing ✅, Banking ✅, Accounting ✅, HR/Payroll ✅, Assets ✅ + Beyond-spec chrome).
> The generic items are ticked as covered there — same gates, same evidence.

- [x] **B-XX** Create `Resources/Controllers/{Module}Controller.en.resx` / `.es.resx` for controller-level titles → shared `CommonMessages` instead (see Phase 2 deviation)
- [x] **B-XX** Refactor module controllers to use `_errors[error.Code]` + `_common[titleKey]`
- [x] **B-XX** Add FluentValidation localization for module validators → n/a, no FluentValidation (see Phase 0 deviation)
- [x] **F-XX** Populate module namespace JSON (en/es) with UI labels
- [x] **F-XX** Migrate module components to `useTranslation('module')` + `useTranslation('error')`
- [x] **F-XX** Verify: module UI renders in both languages, errors translate
- [x] **F-XX** Run build + lint → clean

## Phase 3 — Validation & Polish

- [x] **B-20** Ensure all FluentValidation validators use `IStringLocalizer<ValidationMessages>` → **n/a** (see Deviations: no FluentValidation in the solution)
- [x] **B-21** Ensure all DataAnnotations on DTOs use `ErrorMessageResourceType/Name` → **n/a** (no DataAnnotations validation attributes exist in `src/Backend`)
- [x] **B-22** Backend test: `ValidationLocalization_Es_ReturnsSpanishMessages` → implemented as `Ingest_LeadCodeTooLong_WithEsHeader_ReturnsSpanishDomainValidationError` (domain-validator path)
- [x] **F-20** Frontend test: `LanguageSelector_ChangesLanguageAndPersists`
- [x] **F-21** Frontend test: `ErrorTranslation_ShowsSpanishForKnownCode`
- [x] **F-22** Frontend test: `ErrorTranslation_FallbackToEnglishForMissingKey`
- [x] **F-23** CI: TypeScript compilation fails if `en` namespace missing key (verify via PR check)
- [x] **All** Full end-to-end manual test: switch language, navigate all modules, trigger errors
  → covered by `npm run e2e:language` (real Chrome: en paint → es switch → reload persists,
  zero page errors) + per-module es integration tests + F-17 live test
- [x] **All** Performance check: lazy namespace loading works (network tab shows per-module JSON on demand)
  → verified by traffic capture: boot fetches only `common`+`error`+route namespace (3 files)
  instead of all 12; `ns: ['common', 'error']` in `src/lib/i18n.ts` with the anti-regression comment

---

## Verification Checklist (per task)
Before marking any task `[x]`:

> Applied to every module above (backend `dotnet build` + `dotnet test Erp.sln`, frontend
> `npm run build` + `npm run lint` + `npm test`, new es/coverage/type tests, runtime e2e for F-10).

- [x] Backend compiles (`dotnet build`)
- [x] Frontend compiles (`npm run build`)
- [x] Lint passes (`npm run lint` / `dotnet format --verify-no-changes`)
- [x] Existing tests pass (`dotnet test` / frontend test command)
- [x] New tests added for the task pass
- [x] Manual verification of the specific behavior (language switch, error translation, etc.)

---

## Notes

- **Deviations (Phase 0, approved by evidence)**
  - `B-01`: no new NuGet packages needed — `AddLocalization`/`UseRequestLocalization` come from the ASP.NET Core shared framework.
  - `B-02`/`B-07`: English lives in the **culture-less neutral** files (`ErrorMessages.resx`, `CommonMessages.resx`), not `*.en.resx`, because the .NET satellite chain is `es → neutral` and never `es → en`. Files: `Resources/Shared/ErrorMessages{,.es}.resx`, `Resources/Shared/ValidationMessages{,.es}.resx`, `Resources/Common/CommonMessages{,.es}.resx`.
  - Marker classes are `public sealed class` (not `static`, CS0718) in namespaces matching the resx path: `Erp.Api.Shared.ErrorMessages`, `Erp.Api.Shared.ValidationMessages`, `Erp.Api.Common.CommonMessages`.
  - `B-04`: controllers use a `Text(key, fallback)` extension at `src/Backend/Erp.Api/Localization/LocalizerExtensions.cs` to keep `en` behavior identical when a key is absent.
  - `B-16`/`B-20`/`B-21`: the spec assumes FluentValidation + DataAnnotations validation; **neither exists** in `src/Backend` (15 `PackageReference`s total, zero validation attributes). Validation is domain-owned (`*Validator` static classes → `CRMValidationException`) and localized once at the ProblemDetails boundary. `ValidationMessages.resx` + marker were deleted as dead scaffolding (zero consumers); if a validation framework is ever added, recreate them following the `ErrorMessages` pattern.
  - `F-03`/`F-04`/`F-05`: `error.json` (en/es, 232 codes) and `src/types/i18n.generated.d.ts` are produced by `scripts/i18n-sync.mjs` from the backend resx + the `en` namespaces, never hand-edited. `node scripts/i18n-sync.mjs --check` is the CI gate for `F-23`.
  - `F-05` caveat (verified by breaking it): `CustomTypeOptions` is declared by **`i18next`**, not `react-i18next`, and `strictKeyChecks` defaults to `false`. Augmenting `react-i18next` compiles cleanly while checking nothing — `src/types/i18n.d.ts` sets both, and a deliberate `t('nope.missing-key')` now fails `tsc -b` with TS2345 against `EnCommonKeys`.
  - Catalog completeness is now enforced by `tests/Erp.Api.IntegrationTests/ErrorCatalogCoverageTests.cs`, which reflects every `*ErrorCodes` constant in `Erp.Domain` and asserts it resolves in both the neutral and `es` resources (verified non-vacuous: removing `crm_company_required` fails with that exact code in the message).
- **MSBuild gotcha**: restoring a `.resx` from a backup with `Copy-Item` preserves the *older* `LastWriteTime`, so the incremental build skips recompiling it and tests keep asserting stale strings. Always bump `LastWriteTime` (or `dotnet build -t:Rebuild`) after restoring a resource file.
- **Error code inventory**: Run `grep -r "crm_\|selling_\|purchase_\|stock_\|manufacturing_\|journal_\|hrpayroll_\|banking_\|asset_\|accounting_\|account_\|concurrency_\|financialreport_" src/Backend/Erp.Domain/Entities/*ErrorCodes.cs` to get the complete list for `ErrorMessages.resx`
- **Frontend error.json**: Can be generated from backend `ErrorMessages.en.resx` via script to guarantee parity
- **No migration changes** in this spec — user preference column added in separate auth migration
- **No enum/state renaming** — `OpportunityStatus.Open` etc. remain English in DB and API