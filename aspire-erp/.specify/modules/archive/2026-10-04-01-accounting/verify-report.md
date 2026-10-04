# Verification Report — 01-accounting

**Change**: 01-accounting (re-verify after scope amendment)
**Version**: 2.0.0 (spec.md) + scope amendment 2026-10-04 (`0fa7ee1a`)
**Mode**: Standard (Strict TDD INACTIVE — no `strict_tdd` config anywhere in the repo, no `strict-tdd-verify.md` file → strict-TDD checks not loaded)

## Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 11 (Phase 1: 1.1–1.4, Phase 2: 2.1–2.7) |
| Tasks complete (checkbox `[x]`) | 11 |
| Tasks incomplete | 0 |
| Tasks verified against real evidence (code + passing tests) | 8 fully verified (1.1, 1.2, 1.3, 2.1, 2.2, 2.3, 2.4, 2.5); 2 verified by source inspection only (1.4, 2.6 — no frontend test runner); 1 with acceptance gap (2.7 → W2) |
| Artifacts present | specs ✅ (amended `0fa7ee1a`), plan ✅, tasks ✅, proposal ❌ (absent for all modules, by design), applyProgress = tasks.md checkboxes |
| Spec rows in certified scope | 10 (invariants AC-01…AC-04; scenarios AC-01…AC-04, AC-06, AC-07) |
| Spec rows DEFERRED (out of scope, excluded from scoring) | 3 — invariant AC-05 (FX, `spec.md:49`), invariant AC-06 (period closing, `spec.md:55`), scenario AC-05 (period closing voucher, `spec.md:94`) |
| Skipped dimensions | proposal (artifact absent), coverage measurement (no collector configured), Strict-TDD checks (inactive), frontend automated tests (no test runner in `erp-client`) |

## Build & Tests Execution

**Build**: ✅ Passed (verifier-run)

```text
> dotnet build Erp.sln --nologo -v q   (workdir C:\Workspace\Odoo\aspire-erp, HEAD 1a17c866)
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

First attempt failed with `CS2012 … Erp.Domain.dll … being used by another process` — file-lock contention from a concurrent verifier, **not** a code defect; a retry after 45 s produced the clean result above.

**Scoped unit re-run (verifier, this module only — integration assembly deliberately NOT run)**:

```text
> dotnet test tests/Erp.Domain.UnitTests --filter "FullyQualifiedName~JournalEntry|~CompanyFreeze|~Account|~DoubleEntry"
Passed!  - Failed: 0, Passed: 72, Skipped: 0, Total: 72, Duration: 184 ms

> dotnet test tests/Erp.Application.UnitTests --filter "FullyQualifiedName~JournalEntry|~Account|~Idempotency|~FinancialReport|~DoubleEntry"
Passed!  - Failed: 0, Passed: 72, Skipped: 0, Total: 72, Duration: 368 ms
```

**Full-suite evidence (orchestrator run at this exact code state)**: ✅ 370 passed / ❌ 0 failed — 86 `Erp.Domain.UnitTests` + 231 `Erp.Application.UnitTests` + 53 `Erp.Api.IntegrationTests`, build 0 warnings / 0 errors.

Corroborated by git, independently:

```text
> git log --oneline -8
1a17c866 docs(sdd): amend 03-selling spec to mark deferred scope
0fa7ee1a docs(sdd): amend 01-accounting spec to mark deferred scope
e599ecac docs(sdd): archive 02-stock module and promote spec
99f5d518 docs(sdd): repair 04-buying index links after archive
2460d84e fix(stock): wire stock entry cancellation endpoint ...
55553ce5 test(stock): add adversarial concurrency test ...
```

- Everything after the last source-affecting commit `2460d84e` (`99f5d518`, `e599ecac`, `0fa7ee1a`, `1a17c866`) touches **only** `.specify/` documents — confirmed file-by-file with `git log --stat`.
- No accounting source or accounting test file changed since my previous report; the only delta is the stock cancellation work.
- Test-count arithmetic matches exactly: 361 → 370 = +9 = `CancelStockEntryCommandHandlerTests` (+6, Application) + `StockEntriesCancellationApiTests` (+3, Integration), with Domain flat at 86 → 225+6 = 231, 50+3 = 53. ✅
- `git status --porcelain` clean; HEAD `1a17c8664c794e96655fc3783b18ac9c4e57efc2`.

**Coverage**: ➖ Not available / threshold: not configured → no coverage collector in the solution; recorded as a skipped check.

**Repository state**: verify made no edits, no commits, no `.specify/` writes, no Engram writes.

## Spec Compliance Matrix

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| Invariant AC-01 (`\|ΣD − ΣC\| ≤ 0.0001`, else `DoubleEntryImbalanceException`) | zero-sum tolerance | `Erp.Application.UnitTests > DoubleEntryGuardTests` (6 tests incl. `EnsureBalanced_RoundingWithinFourDecimals_IsAccepted`, `EnsureBalanced_ImbalancedVoucher_ThrowsWithBothTotals`) + `Erp.Domain.UnitTests > JournalEntryTests > EnsureBalanced_WithinTolerance_DoesNotThrow` / `EnsureBalanced_ToleranceExceeded_ThrowsDoubleEntryImbalance`; enforced at `Domain\Services\DoubleEntryGuard.cs:41`, `JournalEntry.cs:158-166` | ✅ COMPLIANT |
| Invariant AC-02 (append-only `GLEntry` + counter-entries with audit reference) | immutability | `Erp.Api.IntegrationTests > GLEntryLedgerGuardTests` (4 tests: `SaveChanges_WhenGLEntryIsModified_ThrowsAppendOnlyViolationAndWritesNothing`, `SaveChanges_WhenGLEntryIsRemoved_…`, `RawSqlUpdate_WhenTargetingPostedRow_IsRejectedByDatabaseTrigger`, `Insert_WithNegativeDebit_IsRejectedByNonNegativeCheckConstraint`) — `AppDbContext.EnforceLedgerAppendOnly` (`AppDbContext.cs:185-196`) + trigger `trg_GLEntry_AppendOnly` (`Migrations\20261002060750_AddStockAndGeneralLedger.cs:396`) | ✅ COMPLIANT |
| Invariant AC-03 (leaf posting accounts only → `InvalidPostingAccountException`) | group-account rejection | `JournalEntryTests > EnsurePostableAccounts_GroupAccount_ThrowsPostingToGroupAccountProhibited` + `JournalEntryPipelineTests > Submit_GroupAccount_FailsWithPostingToGroupAccountProhibitedAndZeroLedgerRows`; thrown at `JournalEntryValidator.cs:84` | ✅ COMPLIANT (wire code literal differs → W3) |
| Invariant AC-04 (freeze date lock) | period lock | `Erp.Domain.UnitTests > CompanyFreezeLockTests` (5 tests incl. `EnsurePostingDateUnlocked_PostingDateEqualsFreeze_Throws`) + `JournalEntryPipelineTests > Submit_FrozenPeriod_FailsWithFiscalPeriodLockedAndZeroLedgerRows` + `PurchasePostingServiceTests > PostInvoiceAsync_PostingDateOnFreezeDate_ThrowsFiscalPeriodLockAndAppendsNoGlRows`; rule at `Company.cs:47-51` | ✅ COMPLIANT |
| Invariant AC-05 (realized FX gain/loss) | — none — | No implementation, no test (unchanged: 0 hits for `ExchangeRate\|RealizedExchange\|ExchangeGainLoss\|Settlement` in `src\Backend`) — **DEFERRED marker `spec.md:49`** | ⚪ OUT OF SCOPE (DEFERRED) |
| Invariant AC-06 (period-closing roll-forward) | scenario AC-05 below | No implementation, no test — **DEFERRED marker `spec.md:55`** | ⚪ OUT OF SCOPE (DEFERRED) |
| Scenario AC-01 (balanced JE → `Submitted`, two balanced rows, net $0.00) | balanced journal entry | `JournalEntriesApiTests > Submit_BalancedDraft_Returns201Then200AndAppendsTwoBalancedLedgerRows` + `JournalEntryPipelineTests > Submit_BalancedDraft_AppendsMirroredLedgerRowsInsideOneTransaction` (asserts `VoucherId`/`VoucherNo` per row) + `FinancialReportsApiTests > TrialBalance_AfterSubmittingBalancedVoucher_ReportsZeroDiscrepancy` (the "$0.00 net" clause) | ✅ COMPLIANT |
| Scenario AC-02 (imbalanced voucher blocked, zero `GLEntry` rows) | imbalance rejection | `JournalEntriesApiTests > Submit_ImbalancedDraft_Returns400ImbalanceAndWritesZeroLedgerRows` + `JournalEntryPipelineTests > Submit_ImbalancedDraft_FailsWithDoubleEntryImbalanceAndZeroLedgerRows` | ✅ COMPLIANT |
| Scenario AC-03 (group account `1000 - Assets` rejected with `PostingToGroupAccountProhibited`) | group account | `JournalEntriesApiTests > Submit_GroupAccountDraft_Returns400PostingToGroupAccountAndWritesZeroLedgerRows` (asserts `posting_to_group_account_prohibited`, zero rows) | ✅ COMPLIANT (literal token differs → W3) |
| Scenario AC-04 (back-dated post rejected, no data modified) | freeze lock | `JournalEntriesApiTests > Submit_WhenPeriodIsFrozen_Returns409FiscalPeriodLockedAndWritesZeroLedgerRows` + `FiscalPeriodLockApiTests > PostStockEntry_WhenCompanyIsFrozen_RejectsOnAndBeforeFreezeDateWith409AndNoNewLedgerRows` | ✅ COMPLIANT |
| Scenario AC-05 (`PeriodClosingVoucher`, `3100 - Retained Earnings` credited $120,000) | year-end close | Re-confirmed absent (grep `PeriodClosing\|RetainedEarnings\|FiscalYear\|YearClose` over `tests\` → 0 hits; no entity in `Erp.Domain\Entities\`; `scripts\seed-dev-coa.sql` seeds only root `3000 Equity`) — **DEFERRED marker `spec.md:94`** | ⚪ OUT OF SCOPE (DEFERRED) — not counted as UNTESTED |
| Scenario AC-06 (idempotent submission guard: replay → HTTP 200 + identical body, no duplicate postings) | idempotency | `Erp.Application.UnitTests > IdempotencyPolicyTests` (7 tests incl. `Decide_SameKeySameBodyCompleted_ReplaysStoredResponse`) + e2e proof of the shared filter only on purchases: `PurchaseInvoiceApiTests > Create_ReplayedWithSameKey_Returns200WithIdenticalBodyAndBooksNothingNew` and `ReceiptCreate_ReplayedWithSameKey_Returns200WithIdenticalBodyAndBooksNothingNew` | ⚠️ PARTIAL — **still no journal-entry replay test**: all 7 `JournalEntriesApiTests` mint a *fresh* key (lines 394, 473), `POST /api/v1/journal-entries` (create, `JournalEntriesController.cs:118`) has no `[IdempotencyKeyRequired]` (guards at `:167` submit, `:204` cancel), spec literal `idemp-jv-2026-009` (`spec.md:103`) appears nowhere in the repo (W2) |
| Scenario AC-07 (cancel → `Cancelled`, swapped reversal, audit reference, originals never mutated) | cancellation & reversal | `JournalEntriesApiTests > Cancel_SubmittedVoucher_AppendsSwappedReversalAndKeepsOriginalsUntouched` + `Cancel_DraftVoucher_Returns409InvalidStatusTransitionAndWritesNoRows` + `JournalEntryPipelineTests > Cancel_SubmittedEntry_AppendsSwappedReversalAndKeepsOriginals` (asserts `reversal.VoucherId == original.VoucherId`, `Remarks` starts `Reversal of JournalEntry` and contains the JV number, original tuple byte-identical — `JournalEntryPipelineTests.cs:543-553`) | ✅ COMPLIANT |

**Compliance summary (in scope)**: 9/10 rows COMPLIANT, 1/10 PARTIAL (scenario AC-06), 0 UNTESTED, 0 FAILING. Plus 3 rows OUT OF SCOPE (DEFERRED by amendment `0fa7ee1a`), excluded from scoring. 13 rows total.

## Correctness (Static Evidence — tasks)

| Task | Status | Notes |
|------|--------|-------|
| 1.1 `Account` entity & tree invariants | ✅ Implemented | `src\Backend\Erp.Domain\Entities\Account.cs` + `AccountValidator.cs` (`EnsureValidFields`, `EnsureValidParent`, `EnsureNoCycle`); 20 `AccountValidatorTests` + 11 `CreateAccountCommandHandlerTests` (cycle, leaf-parent, root-type mismatch, duplicate code) |
| 1.2 `GetAccountTreeQuery` read model | ✅ Implemented | `Features\Accounts\Queries\GetAccountTreeQuery.cs` / `GetAccountTreeQueryHandler.cs` (flat load + dictionary assembly, missing-parent promoted to root); 3 `GetAccountTreeQueryHandlerTests` incl. `Handle_AccountWithMissingParent_IsTreatedAsRoot_NeverDropped`; complexity caveat → S2 |
| 1.3 Accounts API | ✅ Implemented | `AccountsController.cs:38` `[HttpGet("tree")]`, `:57` `[HttpPost]`; `AccountsApiTests > GetTree_WithTenantHeader_Returns200WithTenantScopedTree` + `PostAccount_Returns201AndRoundtripsIntoTree` |
| 1.4 `AccountTreeTable.tsx` | ✅ Implemented (no automated test) | expand/collapse (`aria-expanded`), search filter with auto-expand, `ROOT_TYPE_BADGE_CLASS` root-type badges; wired `App.tsx:30`; source inspection only (package.json has no test script) |
| 2.1 `GLEntry` + zero-sum enforcement | ✅ Implemented | `Entities\GLEntry.cs` + `Domain\Services\DoubleEntryGuard.cs`; non-negative DB check `CK_Debit_NonNegative` (`AppDbContextModelSnapshot.cs:508`); `DoubleEntryGuardTests` (6) |
| 2.2 `FrozenAccountsDate` lock | ✅ Implemented | `Company.EnsurePostingDateUnlocked` (`Company.cs:47-51`), mapped `CompanyConfiguration.cs:33`; 5 `CompanyFreezeLockTests` + API-level 409 tests (matrix rows AC-04) |
| 2.3 `JournalEntry` aggregate & posting pipeline | ✅ Implemented | `Entities\JournalEntry.cs` (status machine, `EnsureBalanced`), `JournalEntryValidator.cs`, `Features\GeneralLedger\JournalPosting.cs`; 27 `JournalEntryPipelineTests` |
| 2.4 Journal Entries API | ✅ Implemented | `JournalEntriesController.cs` — `POST` `:118`, `POST {id}/submit` `:166`, `POST {id}/cancel` `:203`, dual route templates `:43-44`; 7 `JournalEntriesApiTests` |
| 2.5 Trial Balance / Balance Sheet / P&L | ✅ Implemented | `GetTrialBalanceQuery(Handler)`, `GetBalanceSheetQuery(Handler)`, `GetProfitAndLossQuery(Handler)`; 13 `FinancialReportQueryTests` (`TrialBalance_BalancedLedger_ReportsZeroDiscrepancy`, `BalanceSheet_ReportsBalancedWhenTheSectionsCloseTheEquation`, `ProfitAndLoss_SplitsCogsFromOperatingExpensesAndComputesNetProfit`, `BalanceSheetResidual_EqualsProfitAndLossNetProfit`) + 6 `FinancialReportsApiTests`; "validates" caveat → S3 |
| 2.6 `GeneralLedgerOverview.tsx` | ✅ Implemented (no automated test) | voucher drill-down (`VoucherDrillDown`), account/date filters, footer totals over full filtered set, `Balanced` badge (`GeneralLedgerOverview.tsx:308`); wired `App.tsx:34`; source inspection + type-checked build only |
| 2.7 Idempotency-Key (AC-06) | ⚠️ Implemented, acceptance partially verified | `Erp.Api\Filters\IdempotencyFilter.cs`, `IdempotencyKeyRequiredAttribute.cs`, `Application\Common\IdempotencyPolicy.cs`; 7 `IdempotencyPolicyTests` + purchase invoice/receipt replay e2e — but no test replays a journal-entry submission, so the acceptance "return the cached result without duplicating `GLEntry` records" is unasserted for this module's endpoint → W2 |

Evidence integrity note: `git diff 55553ce5..1a17c866 -- src tests` shows only `Erp.Api/Program.cs` (+1) and two new **stock** test files, so every source citation above was re-validated as still accurate at HEAD.

## Design Coherence (plan.md)

| Decision | Followed? | Notes |
|----------|-----------|-------|
| §1 Topology (Domain / Application / Infrastructure / Api + React feature folders) | ⚠️ Mostly | `Account.cs`, `GLEntry.cs`, `JournalEntry.cs`, `Company.cs`, all three named exceptions, `IAccountRepository`/`IGLEntryRepository`/`ICompanyRepository`, DTOs (`AccountDto`, `AccountTreeNodeDto`, `TrialBalanceReportDto`, `GLEntryDto`) and controllers (`AccountsController`, `JournalEntriesController`, `FinancialReportsController`) present. Deviations: `Entities/FiscalYear.cs` (`plan.md:22`) and `JournalEntryForm.tsx` (`plan.md:41`) do not exist (W7) — `FiscalYear.cs` is now consistent with the spec's DEFERRED marker, but the plan still lists it as delivered topology |
| §1 Frontend (`AccountTreeTable`, `GeneralLedgerOverview`) | ✅ Yes | Both exist and are routed in `App.tsx:2,3,30,34` |
| §2 `Account` DDL (temporal + unique tenant/company/code index) | ✅ Yes | `AppDbContextModelSnapshot.cs:102` unique `(TenantId, CompanyId, AccountCode)`, `:108` `IsTemporal` |
| §2 `GLEntry` DDL (indexes, checks, append-only) | ✅ Yes (strictly stronger) | `IX_GLEntry_Tenant_Voucher` (`:497`), `IX_GLEntry_Tenant_Company_Account_Date` + INCLUDE (`:499-502`), `CK_Debit_NonNegative` (`:508`), plus an unspecified DB trigger `trg_GLEntry_AppendOnly` |
| §2 `Company` DDL | ⚠️ Deviation | Plan's GUID FKs (`DefaultReceivableAccountId`, `CostOfGoodsSoldAccountId`, `RoundOffAccountId`, `RealizedExchangeGainLossAccountId`, `RetainedEarningsAccountId`, `plan.md:58-65`) replaced by `*AccountCode` strings; FX and Retained-Earnings columns are now attributable to DEFERRED scope, RoundOff has no counterpart and no deferral marker (`Company.cs:68-103`) (W9) |
| §3 `SubmitJournalEntryCommand` contract | ⚠️ Deviation | Plan: single-shot `(CompanyId, PostingDate, VoucherType, UserRemark, Lines) → Result<Guid>` (`plan.md:131-151`); impl: two-step `SubmitJournalEntryCommand(CompanyId, JournalEntryId, RowVersion) → Result<JournalEntryDto>` (`SubmitJournalEntryCommand.cs:35-38`) + separate `CreateJournalEntryCommand` — impl matches tasks.md 2.4, so the plan document is the stale artifact (W8) |
| §3 Validation chain (balance → freeze → group account) | ✅ Yes | Order preserved and mapped to status codes in `SubmitJournalEntryCommandHandler.cs:105-115` |
| §4 Trial Balance projection (tenant filter, `PostingDate <= @AsOfDate`, group by account, zero-difference check) | ✅ Yes | `GetTrialBalanceQueryHandler.cs` (+ `FinancialReportSections.cs`); covered by unit + API tests |

## Issues Found

### Re-assessment of findings from the previous report

| ID | Previous state | Re-assessed at HEAD `1a17c866` |
|----|----------------|-------------------------------|
| C1 (scenario AC-05 + invariant AC-06 unimplemented/untested) | CRITICAL | **RESOLVED by scope amendment** — `spec.md:55` and `spec.md:94` carry explicit `DEFERRED — scope amendment 2026-10-04 (retro-verify)` markers; row moved to OUT OF SCOPE, excluded from scoring. Evidence of the underlying gap is unchanged (still no code/test), which is now consistent with the declared scope |
| W1 (invariant AC-05 FX unimplemented) | WARNING | **RESOLVED as a scope issue** — `spec.md:49` DEFERRED marker; must be carried forward as a flag in the archive report |
| W2 (scenario AC-06 partial / Task 2.7 acceptance unmet) | WARNING | **OPEN** — unchanged: no JE replay test, create endpoint unguarded |
| W3 (spec literal vs wire code) | WARNING | **OPEN** — `spec.md:85` still says `PostingToGroupAccountProhibited`; wire returns `posting_to_group_account_prohibited` (`JournalEntriesApiTests.cs:200`) |
| W4 (false "100% PRODUCTION CERTIFIED" claim) | WARNING | **PARTIALLY RESOLVED** — spec header fixed (`spec.md:4` now reads "CERTIFIED — scope amended 2026-10-04 …"); root index tables still say `100% CERTIFIED` for 01 (`\.specify\spec.md:13`, `.specify\plan.md:12`, `.specify\tasks.md:12`) — scheduled for the archive commit |
| W5 (tasks.md `IN PROGRESS` + tasks under-scoping the spec) | WARNING | **PARTIALLY RESOLVED** — the "no tasks for deferred features" half is explained by the amendment; `tasks.md:6` still reads `**Status:** IN PROGRESS` with all 11 boxes `[x]` (the 02-stock verify report flagged the identical line as its S1) |
| W6 (duplicate `AC-xx` identifiers for invariants and scenarios) | WARNING | **OPEN** — unchanged (`spec.md:33-61` vs `:67-114`) |
| W7 (plan lists `FiscalYear.cs`, `JournalEntryForm.tsx`) | WARNING | **OPEN, partly explained** — `FiscalYear.cs` now matches the DEFERRED scope, but `plan.md:22` still presents it as delivered topology and `JournalEntryForm.tsx` (`plan.md:41`) remains simply missing |
| W8 (plan §3 command contract drift) | WARNING | **OPEN** — unchanged |
| W9 (plan §2 Company DDL drift) | WARNING | **OPEN, partly explained** — FX/Retained-Earnings columns belong to deferred scope; `RoundOffAccountId` has neither code nor deferral |
| W10 (glossary promises without implementation) | WARNING | **OPEN, partly explained** — glossary rows `spec.md:24` (`Fiscal Year`) and `:25` (`Period Closing Voucher`) were **not** amended even though their invariants/scenarios were deferred; `Payment Ledger Entry` (`:23`) still has no implementation; Cost Center still column-only |
| S1–S6 | SUGGESTION | S1, S2, S3, S5, S6 **open**; S4 (seed `3100 - Retained Earnings`) now **folded into the deferred carry-forward scope** |

**CRITICAL**: None. Build 0 warnings / 0 errors (verifier run), orchestrator full suite 370/370 exit 0 at this code state, every in-scope spec row has a passing covering test, and the three previously untestable rows are explicitly OUT OF SCOPE.

**WARNING**:

- **W2 (in-scope scenario AC-06 partial / Task 2.7 acceptance unmet)** — Journal-entry idempotency is asserted only at unit level (`IdempotencyPolicyTests`, 7 tests) and e2e only for *purchase* endpoints (`PurchaseInvoiceApiTests.Create_ReplayedWithSameKey_…`, `…ReceiptCreate_ReplayedWithSameKey_…`). No test replays `POST /api/v1/journal-entries/{id}/submit`; every request in `JournalEntriesApiTests` mints a fresh key (lines 394, 473). `POST /api/v1/journal-entries` (create, `JournalEntriesController.cs:118`) carries no `[IdempotencyKeyRequired]` (guards only at `:167` and `:204`), so a retried create mints a second draft voucher — narrower than AC-06's literal "retry the identical POST command … returns HTTP 200 with the previously created voucher details" (`spec.md:102-107`, unchanged by the amendment).
- **W3 (spec literal vs wire format)** — `spec.md:85` demands error `PostingToGroupAccountProhibited`; the API returns `posting_to_group_account_prohibited`. Mapping is deliberate and documented (`InvalidPostingAccountException.cs:20-24`, `AccountingErrorCodes.cs:32-38`), but the spec token is never returned verbatim; same pattern for `DoubleEntryImbalanceException` / `FiscalPeriodLockedException` vs `double_entry_imbalance` / `fiscal_period_locked`.
- **W4 (certification claim, partially fixed)** — module spec header corrected (`spec.md:4`), but the root index tables still advertise `` `100% CERTIFIED` `` for 01-accounting (`.specify\spec.md:13`, `.specify\plan.md:12`, `.specify\tasks.md:12`). Fix in the archive commit as planned; until then the repo still over-claims one row.
- **W5 (tasks.md status line)** — `tasks.md:6` still reads `**Status:** IN PROGRESS` although all 11 boxes are `[x]`; flip it in the archive commit (same leftover flagged for 02-stock).
- **W6 (duplicate identifiers in spec)** — invariants `AC-01…AC-06` and scenarios `AC-01…AC-07` share namespaces (`spec.md:33-61` vs `:67-114`); code comments cite them ambiguously (`JournalEntriesController.cs:156`, `SubmitJournalEntryCommand.cs:29` say "spec AC-03/AC-04" without saying which namespace).
- **W7 (plan/impl doc drift)** — `plan.md:41` requires `JournalEntryForm.tsx` and `plan.md:22` requires `Entities/FiscalYear.cs`; neither file exists (`erp-client\src\features\accounting\` holds only `AccountTreeTable.tsx`, `GeneralLedgerOverview.tsx`, `types.ts`, two hooks; `Erp.Domain\Entities\` has no `FiscalYear.cs`). The amendment reconciled the *spec*, not the plan.
- **W8 (plan §3 contract drift)** — plan's single-shot `SubmitJournalEntryCommand(CompanyId, PostingDate, VoucherType, UserRemark, Lines) : ICommand<Result<Guid>>` (`plan.md:131-151`) vs implementation `SubmitJournalEntryCommand(CompanyId, JournalEntryId, RowVersion) : ICommand<Result<JournalEntryDto>>` (`SubmitJournalEntryCommand.cs:35-38`) plus a separate `CreateJournalEntryCommand`. The impl matches tasks.md 2.4, so the approved design document is the stale artifact.
- **W9 (plan §2 Company DDL drift)** — plan's GUID account FKs are implemented as `*AccountCode` strings (`Company.cs:68-103`); `CostOfGoodsSoldAccountId`, `RoundOffAccountId` have no counterpart and no DEFERRED marker (`RealizedExchangeGainLossAccountId`/`RetainedEarningsAccountId` are now covered by the deferral).
- **W10 (glossary promises without implementation)** — `Payment Ledger Entry` (`spec.md:23`) has zero implementation hits; `Fiscal Year` (`:24`) and `Period Closing Voucher` (`:25`) glossary rows were left unamended while their invariants/scenarios were deferred; `Cost Center` (`:27`) exists only as a nullable `GLEntry.CostCenterId` with no master data or validation — posting services hard-code `CostCenterId = null` (`StockPostingService.cs:561`, `SalesPostingService.cs:506`, `PurchasePostingService.cs:689`), though journal lines do accept and mirror it (`CreateJournalEntryCommand.cs:20`, `JournalPosting.cs:110`).

**SUGGESTION**:

- S1 — Add one integration test replaying `POST /api/v1/journal-entries/{id}/submit` with the same `Idempotency-Key`, asserting 200 + byte-identical body + unchanged row count; closes AC-06 and Task 2.7's acceptance in a single test.
- S2 — Task 1.2's "O(N) traversal" acceptance is not literally met nor asserted: `GetAccountTreeQueryHandler.cs:58` and `:85` sort per node (overall O(N log N)). Reword the acceptance or drop the claim.
- S3 — Task 2.5 says `GetTrialBalanceQuery` "validates total debits equal total credits", but the handler only *reports* `TotalDebit - TotalCredit` (`GetTrialBalanceQueryHandler.cs:46-61`); no test asserts a non-zero discrepancy surfaces. Add an imbalanced-ledger fixture.
- S5 — `erp-client/package.json` exposes only `dev/build/lint/preview`; tasks 1.4 and 2.6 are verified by type-check alone. Consider vitest + React Testing Library.
- S6 — Decide whether draft creation should carry `[IdempotencyKeyRequired]` (spec-literal AC-06) or amend AC-06's wording to "ledger-posting transitions are guarded" (the Constitution VI.4 position documented at `JournalEntriesController.cs:21-23`).
- S7 — When archiving, record the three DEFERRED items (FX invariant, period-closing invariant, period-closing scenario) plus the deferred carry-forward of S4 (`3100 - Retained Earnings` seed) as explicit flags in the archive report, as the amendment text itself promises (`spec.md:49,55,94`), and fix W4/W5 in the same commit so no `` `100% CERTIFIED` `` or `IN PROGRESS` leftovers survive.

## Verdict

**PASS WITH WARNINGS**

At HEAD `1a17c866` the build is clean (0 warnings / 0 errors, verifier-run), the orchestrator's full suite is 370/370 at this exact code state (corroborated: only `.specify/` documents changed after the last source commit `2460d84e`, and the +9 test delta matches exactly the two stock test classes added there), and my own scoped re-runs passed 144/144 across `Erp.Domain.UnitTests` and `Erp.Application.UnitTests`. The amendment `0fa7ee1a` legitimately resolves the previous CRITICAL: invariants AC-05/AC-06 and scenario AC-05 are now explicitly `DEFERRED` and out of the certified scope, so they are reported as OUT OF SCOPE rather than UNTESTED. Every one of the 10 in-scope spec rows has a passing covering test except scenario AC-06, which is PARTIAL (unit-level policy coverage plus purchase-endpoint e2e, no journal-entry replay) — a real but narrow gap that maps to a WARNING, not a failure. The nine remaining warnings are documentation/design drift (root index still `100% CERTIFIED`, `tasks.md` `IN PROGRESS`, plan-vs-impl contract and DDL divergence, duplicate `AC-xx` identifiers, unamended glossary rows) plus that single coverage gap; none is a failing or untested in-scope scenario, and all are cheap to close in the archive commit. Compliance summary: **13 rows — 9 COMPLIANT, 1 PARTIAL (scenario AC-06), 3 OUT OF SCOPE (DEFERRED); 0 CRITICAL, 9 open WARNING (W2–W10), 6 open SUGGESTION (S1–S3, S5–S7); previous C1 and W1 resolved by scope amendment.**
