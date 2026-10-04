# VERIFY REPORT — Change `05-banking` (Banking & Reconciliation)

## Completeness (7/7 tasks vs real evidence)

| Task | Evidence (code + passing tests) | Status |
|---|---|---|
| 6.1 `PaymentEntry` + `PaymentAllocation` | `Erp.Domain.Entities.PaymentEntry` (`PaymentType` Receive/Pay, `PaidAmount`, `PaymentDate`, `ClearanceDate`, `RowVersion`), `Allocate()` anti-overpayment guard; `PaymentEntryAllocationTests` (4 tests incl. exact-landing + progressive accumulation) | ✅ CHECKED |
| 6.2 Import + staging | `ImportBankStatementCommand/Handler`, `CsvStatementParser`, `OfxStatementParser`, `BankTransactionValidator`; `BankStatementImportServiceTests` (9) + `StatementParserTests` (16 incl. BN-02 validator cases) + live 1000-line test | ✅ CHECKED |
| 6.3 Rules engine | `BankRuleMatcher` (Domain, dependency-free), `ApplyMatchingRulesCommand/Handler`, `CreateBankTransactionRuleCommand/Handler`; `BankRuleMatcherTests` (16) + `ApplyMatchingRulesTests` (9) + `CreateBankTransactionRuleTests` (7) + live BN-02 test | ✅ CHECKED |
| 6.4 Reconcile | `ReconcileBankTransactionCommand/Handler` + `Unreconcile*` pair, `BankReconciliation` link entity; `ReconcileBankTransactionTests` (23: reconcile + unreconcile classes) + 2 live tests | ✅ CHECKED |
| 6.5 Dialog backend | `CreateVoucherFromBankTransactionCommand/Handler` (GL reuse via `JournalPosting.BuildLedgerLines` + `DoubleEntryGuard`, atomic link tail); `CreateVoucherFromBankTransactionTests` (7) + live BN-04 test | ✅ CHECKED |
| 6.6 React UI | `features/banking/` — `BankingOverview.tsx` (lazy), `BankStatementImporter.tsx`, `BankReconciliation.tsx`, `VoucherQuickCreateDialog.tsx`, `types.ts`; wired in `App.tsx` route, `Sidebar.tsx`, `Header.tsx`, `useNavigationStore.ts`; `npm run build` emits `BankStatementImporter-*.js` + `BankReconciliation-*.js` chunks, lint 0 errors | ✅ CHECKED (behavior-detail audit partial — see WARNING 3) |
| 6.7 Integration tests | `BankStatementImportApiTests` (6 live tests, DB-oracle, per-test bank-account rows, FK-order cleanup), migration `20261004075545_AddBankingModule`, `scripts/seed-dev-banking.sql` (idempotent, deterministic GUIDs) | ✅ CHECKED |

## Build & Tests Execution (run by verifier at HEAD, 2026-10-04)

**`dotnet build Erp.sln --nologo -v q`** (first attempt hit PowerShell `tail` absence + a >120s timeout; retry with 600s timeout):
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:03:18.22
```

**`dotnet test Erp.sln --nologo`** (full suite, background, completed):
```
Passed!  - Failed:     0, Passed:   311, Skipped:     0, Total:   311, Duration: 2 s - Erp.Application.UnitTests.dll (net10.0)
Passed!  - Failed:     0, Passed:   106, Skipped:     0, Total:   106, Duration: 672 ms - Erp.Domain.UnitTests.dll (net10.0)
Passed!  - Failed:     0, Passed:    60, Skipped:     0, Total:    60, Duration: 1 m 24 s - Erp.Api.IntegrationTests.dll (net10.0)
```
Total **477/477, 0 failed** (note: +7 vs orchestrator's quoted 470 = 305+106+59; my HEAD run shows 311+106+60 — see SUGGESTION 1).

**Targeted banking re-runs** (`--filter` over the 9 named classes, live DB `erp-db` reachable on 127.0.0.1:1433 — `Test-NetConnection` returned `True`):
```
Passed!  - Failed:     0, Passed:    74, Skipped:     0, Total:     74 - Erp.Application.UnitTests.dll
Passed!  - Failed:     0, Passed:    20, Skipped:     0, Total:     20 - Erp.Domain.UnitTests.dll
Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6 - Erp.Api.IntegrationTests.dll
```
100/100 banking tests green, including all 6 live API tests.

**Frontend** (`src/Frontend/erp-client`): `npm run build` → `✓ 2028 modules transformed`, `✓ built in 7.65s`, chunks `BankStatementImporter-BEyiXMoM.js (5.19 kB)`, `BankReconciliation-BVdRFpEc.js (49.69 kB)`; `npm run lint` → `Found 3 warnings and 0 errors` on 32 files. No test runner exists — recorded as skipped dimension (same as all prior modules).

## Spec Compliance Matrix

| Invariant / Scenario | Named passing test(s) | Verdict |
|---|---|---|
| BN-01 staging isolation, ΔGL=0 | `Import_CsvWith50Lines_Persists50UnreconciledRowsAndZeroGlWrites` (unit) + live `Import_1000CsvLines_Stages1000UnreconciledWithZeroGLEntries` (1000/1000/0 summary, `COUNT_BIG(GLEntry)` delta 0, 1000 Unreconciled rows) | ✅ PASS |
| BN-02 exclusivity rejections | `Import_CsvWithBothSidesPositive_FailsWithBothSidesPostedAndWritesNothing`, `Import_CsvWithNegativeSide_FailsWithNegativeAmountAndWritesNothing`, `Validator_BothSidesPositive/NegativeSide` (parser tests), `BankTransactionValidator.EnsureValidSides` + DB `CK_Deposit/Withdrawal_NonNegative` | ✅ PASS |
| BN-03 clearance both sides, PostingDate untouched | `Reconcile_DepositAgainstEqualVoucher_ReconcilesBothSidesWithClearance` (asserts `PaymentEntry.PaymentDate == 2026-09-30` unchanged, both `ClearanceDate == 2026-10-02`); live GL-path test asserts line `Reconciled`/40.00/`2026-10-02` + zero GL delta + voucher Dr==Cr | ✅ PASS |
| BN-04 exact-sum + per-account oracles | `Reconcile_OneLineAgainstTwoVouchers_SumsExactlyAndReconcilesAll`, `Reconcile_OffByOneCent_FailsWithAmountMismatchAndWritesNothing`; `QuickVoucher_FeeWithdrawal_PostsBalancedSubmittedVoucherAndReconcilesAtomically` (Dr 5150/Cr bank $15); live `QuickVoucher_FeeLine` asserts `glBefore+2` rows with exact `r.Account==1110/5110 && Debit/Credit==15` rows | ✅ PASS |
| BN-01 scenario (50-line shape) | Covered by 50-line unit test (spec's "50 lines" is the shape; live test proves it at 1000) | ✅ PASS |
| BN-02 rule matching | `RunRule_StripeLine_MarksMatchedWithSuggestions` + priority/scope/inactive/auto-create-report cases (9) + 16 matcher unit tests + live `ImportOfx_ThenRunRules_MatchesStripeLineWithPersistedSuggestions` (matchedCount==1, suggestion persisted, OFX sign→deposit/withdrawal directions) | ✅ PASS |
| BN-03 dual-sided reconcile | Unit (PaymentEntry path) + live (GL-voucher path: 1 link, reconcile appends zero GL rows, then un-reconcile → `Unreconciled`, 0 links) | ✅ PASS |
| BN-04 on-the-fly voucher | Unit + live (201, `Submitted`, balanced Dr 5110/Cr 1110 $15, line `Reconciled`/15.00, 1 link) | ✅ PASS |
| BN-05 FITID dedup counts | `Import_SameOfxFileTwice_SecondImportSkipsAllDuplicates`, `Import_DuplicateFitIdWithinBatch_ImportsFirstSkipsRest` + live `Reimport_SameOfxContent_SkipsEveryFitidAsDuplicate` (5/0 then total 5/imported 0/duplicates 5, staging stays 5) | ✅ PASS |
| BN-06 un-reconcile NULLs | `Unreconcile_ReconciledLine_RestoresNullsAndZeroAndReopensDifference` (Status, AllocatedAmount==0, both ClearanceDate NULL, PaymentDate intact) + live reconcile→unreconcile round-trip | ✅ PASS |
| BN-07 loser's exact code live | `Reconcile_StaleClientRowVersion_*`, `Reconcile_RaceOnTransactionSave_*` (unit) + live `ConcurrentReconciles_SameRowVersion_ExactlyOneWins` (one 200 + one 409, loser body `code=="concurrency_conflict"`, exactly 1 link, line Reconciled, zero GL delta) | ✅ PASS |

## Correctness (per task)

- **6.1**: `Allocate(amount, invoiceOutstanding, alreadyAllocated)` enforces `>0` (`invalid_allocation_amount`) and cap (`over_allocation`, exact-landing allowed). Reconcile handler reuses the `over_allocation` code with cumulative consumption (`consumed + requested > PaidAmount`), covering multi-line-against-one-voucher. Correct.
- **6.2**: Handler validates account/company scope, parses CSV/OFX (format switch case-insensitive, unknown → `invalid_statement_format`), enforces BN-02 per line pre-write, dedups by FITID against DB + within-batch, writes batch header + rows in one transaction. Zero GL reference on the import path (BN-01 by construction; proven by ledger-delta oracles). Correct.
- **6.3**: First-match-wins per transaction, account-scoped-before-global + priority ordering from repository, inactive filtered at repo, no-match leaves NULL suggestions, `AutoCreateVoucher` only reported (`RequiresVoucherCreation`), whole run in one transaction with `concurrency_conflict` surfacing. Correct.
- **6.4**: Zero-write validation ordering (all rejections before mutation), BN-04 exact equality (off-by-cent fails), PaymentEntry path checks company scope + cumulative over-consumption, GL path is read-only via `IGLEntryRepository` page query with exact `SUM(Debit)` match (`gl_amount_mismatch`), line shape xor (`PaymentEntryId xor GlVoucherId`), header-before-links save order, compare-and-swap RowVersion + repo translation both map to `concurrency_conflict`. ClearanceDate := TransactionDate on both sides; PaymentDate never written. Correct.
- **6.5**: Zero-write validation incl. frozen-period gate (`EnsurePostingDateUnlocked`), expense-code resolution (missing/ambiguous → `expense_account_not_found`), postable-accounts guard, direction-aware Dr/Cr (withdrawal → Dr expense/Cr bank), `EnsureBalanced` + `Submit` + `BuildLedgerLines` + `DoubleEntryGuard` reuse (no ledger logic duplicated), voucher-first-then-tail persist order, atomic via ambient shared `AppDbContext` transaction. Correct.
- **6.6**: All four surfaces exist and are wired (route `banking`, sidebar `Banking & Rec`, header category, store union, lazy `BankingOverview` composing importer + reconciliation grid + dialog). Dialog posts `companyId/expenseAccountCode/amount/memo/rowVersion` with fresh `Idempotency-Key`, prefills `|Deposit−Withdrawal|`, surfaces server error codes. DTO↔`types.ts` shapes match (base64 `rowVersion` echoed verbatim; `byte[]?` binds from base64 JSON). `npm run build` + lint 0 errors. Correct modulo WARNING 3.
- **6.7**: 6 live tests, DB-oracle methodology (delta, never absolute), `LedgerMutatingCollection` serialization, per-test bank-account rows with FK-order cleanup, seeded 1110/5110 leaves, idempotency-race fix (`d659d979 detach dead entities before retrying idempotency release`) covered by BN-07 live race. Correct.

## Design Coherence (plan.md)

- **DDL vs EF configs + migration `20261004075545_AddBankingModule`**: BankAccount temporal + `BankAccountHistory` ✅, `BankStatementImport` header ✅, `BankTransaction` decimal(18,4)/CKs/enum-NAME/`rowversion`/tenant-account-status index `IX_BankTransaction_Tenant_Account_Status` ✅, `BankTransactionRule` ✅ — all present in migration (temporal annotations, `rowversion` columns, both CKs, all `IX_Bank*` indexes verified by grep).
- **`BankReconciliation` link table** (plan §1 has none): JUSTIFIED deviation — BN-04 multi-voucher slices need a home, GLEntry is append-only (Constitution III.2), documented in entity + config remarks, cascade-on-transaction-delete / restrict semantics sane, both lookup indexes present.
- **`BankStatementImport.ImportedCount/DuplicateCount`** (plan tracks only raw count): JUSTIFIED deviation — BN-05 summary requires it, documented in entity + config remarks, migration defaulted.
- **`BankRuleMatcher` vs plan §2**: VERBATIM semantics (case-insensitive Contains/StartsWith/Regex-IgnoreCase, AmountEquals `|Deposit−Withdrawal| == amt` exact decimal) plus hardened superset (null-guards, invalid-regex/unparsable-amount → false, never throw) — each hardening branch unit-tested. COMPLIANT.
- **Error codes SCREAMING_SNAKE (plan) vs snake_case (`BankingErrorCodes`)**: JUSTIFIED — implementation follows the repo standard (`StockErrorCodes`, `ConcurrencyErrorCodes.concurrency_conflict` asserted live); plan's thin spot was pre-flagged.
- **Enum persistence**: NAME-as-nvarchar throughout (Status, ConditionType, CounterpartType, ImportStatus) + `JsonStringEnumConverter` — matches `StockEntryType` precedent. Temporal history, `NEWSEQUENTIALID()`, `SYSDATETIMEOFFSET()` defaults — all per plan.
- **DI composition root**: all 6 command handlers + 2 query handlers + `IBankRepository` + both parsers registered (`Program.cs:157-172`); every controller endpoint dispatches a registered handler — no dead route (prior cycle's failure mode explicitly audited).
- **Idempotency**: `[IdempotencyKeyRequired]` on import, run-rules, reconcile, unreconcile, quick-voucher. Rule create/list without it is consistent with master-data precedent (`Accounts/Customers/Items/Warehouses` controllers document the same VI.4 scoping). OK.
- **Frontend/backend contract**: rowVersion base64 round-trip, `netAmount` == BN-04 server expectation, fresh idempotency keys per submission. Coherent.

## Issues Found

**CRITICAL**: None. Full suite green (477/477), every invariant + scenario has named passing tests with exact oracles, no unchecked task, no red dimension except the universally-skipped frontend runner.

**WARNING 1** — Stale spec status header (docs drift): `.specify/modules/05-banking/spec.md:4` still reads "IN PROGRESS — Block A implemented (tasks 6.2 partial); Block B/C pending" while `tasks.md` marks 7/7 `[x]` and the code/tests prove all blocks landed. Fix the header to implemented/verified before archive, lest a future cycle re-litigate scope.
**WARNING 2** — Frontend lint warnings (3 warnings, 0 errors): one is in-scope (`BankReconciliation.tsx:56` set-state-in-effect; React Compiler skipped optimizing that component). Non-blocking, but clean it with the module owner.
**WARNING 3** — Task 6.6 UI behavior details only partially line-audited: wiring, contracts, build, and lint verified; drag-and-drop/column-mapping-preview/filter specifics were not individually exercised (no frontend runner exists). Code + build + API-contract evidence is strong, but record 6.6's interactive details as build-verified rather than test-proven.

**SUGGESTION 1** — Suite-count bookkeeping: my HEAD run totals 477 (311+106+60) vs the orchestrator's quoted 470 (305+106+59). Confirm whether +7 delta is new banking tests landed after the quote or a counting difference; either way green.
**SUGGESTION 2** — Back-fill `plan.md` with the as-built controller/API + `BankReconciliation` design (it currently specifies neither); the implementation is the de-facto spec and a faithful plan update would close the loop.

## Verdict: PASS WITH WARNINGS

Reasons: 7/7 tasks evidenced by code + 100/100 banking tests green; full suite 477/477 with zero regressions; all 4 invariants and 7 scenarios mapped to named passing tests with exact oracles (1000-line zero-GL proof, FITID counts, dual-sided clearance with PostingDate/PaymentDate untouched, exact-sum + per-account ledger rows, NULL-restore un-reconcile, live 200/409 `concurrency_conflict`); plan deviations (link table, snake_case codes, summary counts, matcher hardening) are each documented and justified; DI complete with no dead route. Warnings are docs-drift (spec header), lint warnings, and the inherent no-frontend-runner UI caveat — none block certification of backend completion. Recommend: fix WARNING 1's header line, then proceed to Spec Kit verify + archive.

**Compliance summary counts**: tasks 7/7 checked · invariants 4/4 tested · scenarios 7/7 tested (0 UNTESTED, 0 PARTIAL) · backend tests 477/477 pass (banking-targeted 100/100: 74 Application + 20 Domain + 6 live Integration) · frontend build pass + lint 0 errors (3 warnings) · frontend tests skipped (no runner) · CRITICAL 0 / WARNING 3 / SUGGESTION 2.
