# VERIFICATION REPORT — Change `08-crm` (CRM & Sales Pipeline)

**Verifier role:** VERIFY EXECUTOR, re-verification round (read-only; nothing fixed, nothing written).
**HEAD under test:** `6d684745` (fix-pass: backend `065eb53e`, frontend `3d679e75`, tests `6d684745`).
**Standard:** Standard verify (strict TDD inactive). No MSB3021/MSB3027 lock errors; no stale PIDs observed.
**Prior round:** FAIL with 2 CRITICALs (C1 unreachable board, C2 unbuilt SalesOrder clause) + 5 WARNINGs + 3 SUGGESTIONs. This round re-verifies everything from scratch at the fixed tree.

## 1. Completeness (7/7 ticks vs real evidence)

| Task | Tick | Verdict | Evidence |
|---|---|---|---|
| 11.1 Lead Domain Entity & Lifecycle | `[x]` | VERIFIED | `Lead.cs` + `MarkAsConverted` double-convert guard; `LeadValidator` email/phone; temporal `Lead` + `UQ_Lead_Tenant_Company_Code`; unit + live intake proof green |
| 11.2 Opportunity & Weighted Forecasting | `[x]` | VERIFIED | `Opportunity.WeightedAmount` CLR, `PipelineForecaster` verbatim vs plan §3; milestone sync Prospecting 10 / Qualification 25 / Proposal 50 / Negotiation 80; `AdvanceOpportunityStageTests` (15) + live weighted assertions at every stage |
| 11.3 Mandatory Loss Reason | `[x]` | VERIFIED | `MarkAsClosedLost` rejects null/empty/whitespace; domain unit + handler theory + live 400 `crm_loss_reason_required` with zero-write proof |
| 11.4 Lead→Customer Conversion + audit trail | `[x]` | VERIFIED | Customer + Opportunity (Qualification/25%) + `Converted` + conversion Note activity in one transaction; 4 handler unit tests + live (`Converted` status, `HasConversionNoteAsync`, weighted 3750). Residual: note omitted when no author attributable (validator forbids empty author) — documented, acceptable |
| 11.5 Kanban board + drag→forecast + SalesOrder 1-click | `[x]` | VERIFIED (C1+C2 closed) | Route-reachability audit: `useNavigationStore.ts` (`'crm'` in `NavRoute` + validRoutes) + `Sidebar.tsx` (`CRM & Pipeline`) + `App.tsx` (import + render branch) + `Header.tsx` title — route AND render exist. `vite build` emits `OpportunityKanbanBoard-Dfz4ciuN.js` (6.61 kB). `crmApi` URLs verified literally against both controllers — all match real routes/methods |
| 11.6 Idempotent intake + Re-open | `[x]` | VERIFIED | Dedup triple pre-check + DB backstop; live replay + race + reopen tests green |
| 11.7 Unit + Integration tests | `[x]` | VERIFIED | `CrmLifecycleApiTests` 9/9 live; 44 application + 7 domain CRM-targeted; full suite 812/812 |

**Completeness count: 7/7 verified.** The `create-sales-order` endpoint rides implicitly under 11.5/CRM-02 (2 live + 4+ unit tests); acceptance text never names it — recorded as WARNING W-NEW, not a tick-blocker.

## 2. Build & Tests Execution (run at HEAD by the verifier)

- `dotnet build Erp.sln --nologo -v q` → **Build succeeded. 7 Warning(s)** (pre-existing nullable CS8602/CS8618 in Assets handler + CRM test fakes; zero in CRM production code), **0 Error(s)**.
- `dotnet test Erp.sln --nologo` → **242/242 Domain + 489/489 Application + 81/81 Integration = 812/812, 0 failed** (matches orchestrator counts exactly).
- Targeted: Domain `*PipelineForecaster*|*Opportunity*` → **7/7**; Application `*Crm*|*Lead*|*Opportunity*|*Advance*|*Convert*|*Ingest*|*Reopen*|*CreateOpportunitySalesOrder*` → **44/44**; Integration `CrmLifecycleApiTests` → **9/9**; Integration `*SalesOrder*` (delegation no-regression) → **14/14**.
- `npx vite build` → success, CRM chunk **EMITTED** (`OpportunityKanbanBoard-Dfz4ciuN.js 6.61 kB`).
- `npx tsc --noEmit` → exit 0, **zero errors**.
- `npm run lint` → **0 errors, 3 warnings**, all pre-existing OUTSIDE CRM (buying ×2, banking ×1). Zero warnings in `features/crm/**`.
- Dev DB (`erp-db` @ 127.0.0.1:1433, SELECT-only): `UQ_Lead_Company_Source_ExternalRef` EXISTS (unique, filtered `[ExternalReference] IS NOT NULL`); `__EFMigrationsHistory` contains `20261005045803_AddCrmModule` AND `20261005053823_AddLeadDedupUniqueIndex`; `Lead`+`Opportunity` both `SYSTEM_VERSIONED_TEMPORAL_TABLE`; seeds present (`LEAD-DEMO-001`, `OPP-2026-09901`).

## 3. Spec Compliance Matrix

| Spec row | Verdict | Named proof |
|---|---|---|
| CRM-01 weighted math (Won=100%, Lost=0%) | PASS | `PipelineForecasterTests` + `OpportunityTests.WeightedAmount_*` + advance tests + live full-lifecycle (3750→7500→12000→15000) |
| CRM-02 loss reason (`crm_loss_reason_required`) | PASS | Domain theory (null/empty/whitespace), handler theory + zero-write, live 400 + row-untouched |
| CRM-03 conversion traceability | PASS | Handler tests (Converted + Note copy + caller-author precedence) + live (`Converted` + `HasConversionNoteAsync`) |
| CRM-01 scenario literals ($15k/Qualification/25%→$3,750) | PASS | Defaults `Qualification`/`25m` literal; `ShouldOpenAtQualificationMilestone_WhenNoTermsGiven` + live with EXACT literals |
| CRM-02 progression to Won + 1-click SalesOrder (C2 closed) | PASS | Live `CreateSalesOrder_WonDeal_Returns201DraftOrderLinkedByNote` (201, `SO-YYYY-NNNNN`, Draft, totals == amount, customer owns exactly 1 order, linkage Note) + `CreateSalesOrder_OpenDeal_Is409WithZeroNewOrders` (`crm_opportunity_not_won`, zero new orders) + 4+ handler unit tests |
| CRM-03 lost-deal loss reason | PASS | Live `CloseRules_…` (400 → Lost 0%/0 → 409 `invalid_status_transition`, row-untouched at each rejection) |
| CRM-04 idempotent ingestion | PASS (W1 closed) | Live replay (byte-identical 200 + `duplicate=true`, zero new rows) + live concurrent-ingest race (one 201 + one 200-same-id, exactly 1 row via UNIQUE index) + `DuplicateLeadException` 2601/2627 translation |
| CRM-05 re-open (Negotiation/50%, history preserved) | PASS (W3 closed) | Live (Negotiation/Open/50%, reason cleared, activity count unchanged, `OpportunityHistory` Lost+reason row asserted — zero production-code change) |
| CRM-06 RowVersion concurrency | PASS | Handler pre-check unit + repo-conflict mapping unit + live same-RowVersion race (one 200, loser 409, row == winner) |
| S3 reopen probability guard (new) | PASS | Handler theory (boundaries 0/100 accepted) + live `Reopen_OutOfRangeProbability_Is400WithZeroWrites` |

**Spec count: 10/10 PASS; 0 UNTESTED, 0 PARTIAL.**

## 4. Correctness / Design Coherence / Cross-cutting

- C2 design: `CreateOpportunitySalesOrderCommandHandler` owns ONLY CRM preconditions (exists, company-scoped, `Status==Won` else `crm_opportunity_not_won`, customer resolution direct→converted-lead fallback else `crm_opportunity_no_customer`); money/numbering delegated to EXISTING `CreateSalesOrderCommandHandler`. DI registered (`Program.cs:245`); live-exercised. `[IdempotencyKeyRequired]` on the new endpoint + all four other CRM mutations. Linkage gap (no `OpportunityId` FK) honestly recorded — response + Note activity carry it.
- Plan fidelity: temporal tables + History, CHECKs, `RowVersion` coexisting with temporal, read indexes, gapless `OPP-YYYY-NNNNN` via UPDLOCK/HOLDLOCK, forecaster verbatim, enums as NVARCHAR names. Deviation (documented, accepted): error catalog lives at `Erp.Domain.Entities.CRMErrorCodes` snake_case, not plan §2 `Erp.Domain.Crm.Errors` SCREAMING — behavior stable and live-locked (see W-PLAN).
- Company-scoping on every handler + tenant global filter; CRM writes no GL rows (frozen-period N/A re-confirmed). No regressions: full 812 green + selling 14/14.

## 5. Issues Found

**CRITICAL — none.** Both prior CRITICALs closed with live evidence.

**WARNING**
- W-NEW: `create-sales-order` endpoint has no explicit task acceptance line (rides under 11.5 implicitly). Tests cover it; tasks text names it only via the fix-pass closure note added at archive.
- W-SPEC-HEADER (fixed at archive): `spec.md` header read stale "IN PROGRESS — Block A … Block B/C pending"; corrected to IMPLEMENTED & VERIFIED in the archive commit.
- W-SPEC-CODES (fixed at archive): spec §2/scenario text cited `DomainValidationException("LossReasonIsRequired")`; corrected to `CRMValidationException("crm_loss_reason_required")` to match live-locked behavior.
- W-PLAN (carried forward): plan §2 error-catalog namespace/values (`Erp.Domain.Crm.Errors`, SCREAMING) disagree with shipped `Erp.Domain.Entities.CRMErrorCodes` snake_case. Behavior stable + tested; plan left unchanged per no-post-hoc-rewrite rule.

**SUGGESTION (carried forward, no action this change)**
- S-LINKAGE: future `OpportunityId` FK/nullable column on SalesOrder so deal→order trace is queryable, not note-based.
- S-REOPEN-ENTITY: `Opportunity.Reopen()` accepts any decimal (range guard lives only in the handler); consider mirroring the 0–100 guard in the entity.
- S-LINT-NEIGHBOR: 3 lint warnings in buying/banking — outside this change, left for owning modules.

## Verdict: **PASS WITH WARNINGS**

All 7 tasks EARN their ticks, every spec row has named green tests (unit + live), build 0 errors, suite 812/812, vite CRM chunk emitted, tsc clean, lint 0 errors / 0 CRM warnings, dedup index in migration list AND in DB with green live race, DI + idempotency + RowVersion + scoping all exercised live.

**Compliance summary: 10/10 spec rows PASS; 0 UNTESTED, 0 PARTIAL; 0 CRITICAL, 4 WARNING (2 fixed at archive, 2 carried forward), 3 SUGGESTION; suite 812/812 (242 Domain + 489 Application + 81 Integration).**
