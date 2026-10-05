# VERIFICATION REPORT — Change `09-hr-payroll` (Human Resources & Payroll)

## 1. Completeness (5/5 tasks vs real evidence)

| Task | Claimed | Evidence (code + passing tests) | Verdict |
|------|---------|----------------------------------|---------|
| 12.1 Employee Directory & Org Structure | `[x]` | Entities `Employee` (temporal, `IsEligibleForPeriod`), `Department`, `Designation` + validators; `EmployeeEligibilityTests` (10 facts incl. inclusive boundaries), `EmployeeValidationTests`, live mixed-eligibility test | DONE |
| 12.2 Salary Component & Structure Config | `[x]` | `SalaryComponent` (leaf GL guard), `SalaryStructure`+lines+assignments, `PayrollCalculator.CalculateStructureTotals`; `SalaryStructureTests` (17 facts: group-account rejection, overlap, zero-write proofs) + `PayrollCalculatorTests` (7 facts) | DONE |
| 12.3 Monthly Payroll Batch Engine | `[x]` | `SubmitPayrollRunCommandHandler` (eligibility, pricing via calculator, replay-safe inserts, overlap guard, gapless PE numbering); `PayrollBatchTests` (21 facts: HR-01 identity, clamp, proration, pct lines, window edges) + 5 live API tests | DONE |
| 12.4 Two-Phase Accounting Integration | `[x]` | Accrual (Dr earnings / Cr deductions / Cr 2150) in submit handler, `DisbursePayrollCommandHandler` (Dr 2150 / Cr bank), `CancelPayrollCommandHandler` (mirror); unit + live proofs of payable-zero, mirror, 409 terminal guards | DONE |
| 12.5 React HR Directory & Payroll Studio UI | `[x]` | `src/Frontend/erp-client/src/features/hr-payroll/`: `types.ts`, `useHrPayrollData.ts`, `EmployeeDirectory.tsx`, `HrPayrollOverview.tsx`, `PayrollWorkbench.tsx`; `vite build` emits `EmployeeDirectory-*` + `PayrollWorkbench-*` chunks cleanly; 4 GET master routes back the UI | DONE |

All 5 tasks check out against executed evidence. No unchecked task.

## 2. Build & Tests Execution (run at HEAD by this verifier, nothing else running)

### `dotnet build Erp.sln --nologo -v q`
```
Build succeeded.
    6 Warning(s)   (all pre-existing: CancelDisposeAsset CS8602, ReopenOpportunity/ConvertLead test-nullability — none under HrPayroll)
    0 Error(s)
Time Elapsed 00:01:00.04
```
No MSB3021/MSB3027 file-lock errors encountered; no retry needed; no stale process to report.

### `dotnet test Erp.sln --nologo` (full suite)
```
Passed!  - Failed: 0, Passed: 233, Skipped: 0, Total: 233 - Erp.Domain.UnitTests.dll (net10.0)
Passed!  - Failed: 0, Passed: 448, Skipped: 0, Total: 448 - Erp.Application.UnitTests.dll (net10.0)
Passed!  - Failed: 0, Passed:  72, Skipped: 0, Total:  72 - Erp.Api.IntegrationTests.dll (net10.0, Duration: 1m16s)
```
Total **753/753 green** (233 + 448 + 72), matching the orchestrator's last-run count exactly.

### Targeted re-runs by name
```
Domain filter (PayrollCalculatorTests|EmployeeEligibilityTests|EmployeeValidationTests):  Passed 42/42
Application filter (PayrollBatchTests|SalaryStructureTests):                              Passed 37/37
Integration filter (PayrollLifecycleApiTests, LIVE dev DB 127.0.0.1:1433):                Passed 5/5 (Duration: 17s)
```

### Frontend (`src/Frontend/erp-client`)
- `npx vite build` → `✓ built in 2.90s`, emits `EmployeeDirectory-C7Y1dnuK.js (3.44 kB)` and `PayrollWorkbench-DyMg_FsK.js (10.69 kB)` chunks. Clean.
- `./node_modules/.bin/tsc --noEmit` → **EXIT:0, zero errors** (no output). NOTE: the brief expected the gate to be RED from foreign `features/crm/` files — at this HEAD `tsc` is fully GREEN, so there is no crm attribution to confirm. Nothing to fix either way.
- `npm run lint` → `Found 4 warnings and 0 errors`. Warning files: `features/crm/components/OpportunityKanbanBoard.tsx`, `features/buying/BuyingOverview.tsx` (x2), `features/banking/BankReconciliation.tsx`. **Zero warnings under `features/hr-payroll/`**; no NEW warnings from this module.

## 3. Spec Compliance Matrix

| Invariant / Scenario | Named passing test(s) | Verdict |
|----------------------|------------------------|---------|
| HR-01 net identity + floor-at-zero + surplus deferred | `PayrollCalculatorTests.CalculateSalarySlip_Hr01Literal_ReturnsFourThousandNet` (4000/1000/600/400 → 5000/1000/4000), `…_DeductionsExceedGross_ClampsToZero`, `PayrollBatchTests.Submit_Prices_Hr01_Identity_Per_Slip`, `…_Clamps_Net_At_Zero_And_Absorbs_Surplus_In_Voucher`, live `FullCycle_…` slip exact (5000/1000/4000, 4 itemized lines) | **PARTIAL — documented drift**: floor proven everywhere; "surplus deferred" is NOT implemented — surplus is ABSORBED (voucher credits only effective deductions pro-rata). Calculator XML remark + submit-handler comment document this. Spec text still says "deferred". |
| HR-02 accrual (Dr Gross / Cr tax / Cr pension / Cr 2150 net) + Submitted | `PayrollBatchTests.Submit_Posts_Hr02_Exact_Four_Line_Accrual` (20 emp → 100000/12000/8000/80000, 4 GL rows, balanced), live `FullCycle_…` ledger exact (Dr 5130 5000 / Cr 2220 600 / Cr 2225 400 / Cr 2150 4000, +6 GL delta) | **PARTIAL — documented drift**: debited account is **5130**, not spec's **5110** (5110 = Office Supplies, asserted by 01-accounting tests; seed + migration comments record the pick). All literals otherwise exact. |
| HR-02 Phase 2 disburse + payable-zero + Paid | `PayrollBatchTests.Disburse_Posts_Exact_Pair_And_Marks_Paid` (Dr 2150 4000 / Cr 1110 4000, payable net 0), live `FullCycle_…` (pair exact, `ReadAccountNetAsync(2150) == 0`, status Paid) | PASS |
| HR-03 eligibility skip-and-report | `EmployeeEligibilityTests` (8 cases incl. both inclusive boundaries), `PayrollBatchTests.Submit_Filters_Ineligible_And_Reports_Excluded` + `…_Respects_Assignment_Window_Edges`, live `Submit_MixedEligibility_PricesOnlyMariaAndReportsThreeSkips` (1 slip, 3 skips, zero slips for excluded) | PASS |
| HR-04 idempotent replay (200, cached, zero duplicates) | Live `SubmitReplay_SameKeyTwice_SecondIs200ByteIdenticalWithZeroNewRows` (byte-identical body, GL delta 0, 1 entry) via `IdempotencyFilter`; `IdempotencyPolicyTests` (generic filter contract) | **PARTIAL**: submit-run replay proven live byte-identical. Disburse- and cancel-endpoint replays carry the same `[IdempotencyKeyRequired]` attribute but have **no dedicated replay test** (live or unit) proving zero-new-rows on a replayed disburse/cancel key. |
| HR-05 cancel run + mirror + slips Cancelled + terminal guards | `PayrollBatchTests.Cancel_Mirrors_Accrual_And_Cancels_Slips` (per-account swap, `IsCancelled` flags, slips Cancelled, payable-zero), `…_Rejects_Double_Cancel_And_Paid_Run…` + `…_Frozen_Date_And_Stale_RowVersion…` (zero-new-write proofs), live `Cancel_AfterSubmit_…_ThenCancelAfterPaidIs409` (8 rows, mirror swap per account, slips Cancelled, cancel-Paid → 409 `invalid_status_transition`, GL delta 0) | PASS (originals asserted byte-identical only in unit fake; live asserts swap + count + flags, not byte-identity of originals — sufficient) |
| HR-06 unique (PayrollEntryId, EmployeeId) + row locks, exactly one slip | Migration unique index `UQ_SalarySlip_Entry_Employee` + `LockEntrySlipsAsync` in submit path; `PayrollBatchTests.Repository_Rejects_Duplicate_Slip_With_Typed_Conflict` + `Submit_Rejects_Overlapping_Period_With_Zero_Writes` + `Submit_Allows_Rerun_After_Cancel…`; live `ConcurrentSubmits_SamePeriod_ExactlyOneWinsWithOverlapConflict` (1 winner Submitted + 1 slip, loser 409 `payroll_period_overlap`, `CountEntriesForPeriod == 1`, zero Draft leftovers) | **PARTIAL**: same-period/same-employee *cross-entry* race proven live. Same-entry *intra-run* duplicate insert is proven only at fake-repository level (typed `duplicate_salary_slip`); no live concurrent same-(entry,employee) insert test. The DB unique index is the authority and exists in the applied migration, so residual risk is low. |

## 4. Correctness (per task)

- **12.1**: `IsEligibleForPeriod` implements HR-03 literally (Active && joining<=end && (relieving null || >=start), inclusive both ends — boundary tests pass). Enum persistence as NAME strings matches plan DDL `NVARCHAR(20/30)`. Temporal `Employee`/`EmployeeHistory` present in migration. `IsActive` correctly excluded from eligibility per comment.
- **12.2**: Component create rejects group/inactive/unknown accounts with zero writes (17 SalaryStructureTests green). Structure-line math routes through `PayrollCalculator.CalculateStructureTotals` single-line inputs — no reimplementation. Assignment overlap rejected at create; adjacent windows allowed. Percentage base = sum of fixed earning amounts (documented caller contract).
- **12.3**: Submit is one `ExecuteInTransactionAsync` unit: period validation → fiscal gate → numbering lock → overlap guard → entry → slip-range lock → price → replay-safe inserts → totals → accrual → `Submit()`. Zero-slip runs fail loud (`no_eligible_employees`), never posting an empty voucher. Gapless `PE-YYYY-NNNNN` + `PYR-YYYY-NNNNN` asserted (`PE-2026-00001`, `PYR-2026-` prefix) in unit tests.
- **12.4**: Accrual omits zero-amount component lines but always posts the payable line; `DoubleEntryGuard.EnsureBalanced` before every save (accrual, disburse pair, mirror). Disburse reads bank GL via 05-banking `BankAccount.GLAccountId` read-only (no PaymentEntry voucher — documented boundary). Cancel mirrors every line incl. payable, flags reversals `IsCancelled=true`, leaves originals untouched; Paid/Draft cancels → 409 with zero writes. RowVersion enforced on disburse/cancel (stale → `ConcurrencyConflictException` → 409); submit path relies on numbering/overlap locks + transaction rollback (RowVersion race covered by `FailNextEntryUpdate` unit proof).
- **12.5**: UI hooks mirror backend DTO shapes (`types.ts` documents the mapping incl. enum-NAME + DateOnly-string); submit/disburse/cancel all mint fresh `Idempotency-Key` via `crypto.randomUUID()`; eligibility mirrored client-side for display only. Four master GETs + list/detail reads back the views. No HR UI warnings/errors from lint/tsc/build.

## 5. Design Coherence (plan.md)

- **Calculator verbatim (§2)**: `CalculateSalarySlip` matches the plan listing line-for-line (gross/allowances/deductions → `Math.Max(0, gross-ded)`). `CalculateStructureTotals` is a documented additive extension (fixed + %ofBase split by side), not a deviation from the core.
- **DDL vs migration `20261005021545_AddHrPayrollModule`**: all 6 plan tables present with plan types preserved (DateOnly→`date`, RowVersion→`rowversion`, enums→`nvarchar`, temporal Employee with `EmployeeHistory`, `UQ_Employee_Tenant_Company_Code`). Justified deltas, each documented in code: (a) `SalaryStructure`/`SalaryStructureAssignment` tables added (plan DDL lacks them — assignment overlap + window logic needs them); (b) `SalarySlipLine` added (itemization the plan's slip lacks); (c) totals STORED not computed (SQL computed column cannot express the `Math.Max` floor); (d) `Company.PayrollPayableAccountCode` code-not-FK (D3, mirrors coa-seed shape); (e) `PayrollEntry` unique `(Tenant,Company,PayrollNumber)` + slip unique `(PayrollEntryId,EmployeeId)` + overlap guard (HR-06 live provability); (f) 5130 for salary expense (5110/5120/5210 taken — seed file verifies). No unjustified drift found.
- **Cross-cutting**: DI registers ALL hr-payroll handlers — 3 master commands + submit/disburse/cancel + 6 query handlers (`Program.cs:217-229`) — plus `IdempotencyFilter`. `[IdempotencyKeyRequired]` on all three GL-posting mutations (submit/disburse/cancel), reads exempt (correct — they write no rows). `PayrollProblem` maps not-found→404, transitions/overlap/frozen-period/concurrency→409, rest→400 with stable machine codes. Frozen-period gates on all three posting dates; locks on slip-insert path (`LockEntrySlipsAsync`) + numbering `UPDLOCK/HOLDLOCK`. Full 753-suite green → no regressions.

## 6. Issues Found

### CRITICAL
- None. Full suite green (753/753 incl. 5 live), every spec scenario has at least one executed proof, all 5 tasks evidenced.

### WARNING
1. **Spec text drift — surplus "deferred" (HR-01)**: spec invariant + glossary still promise deferral; implementation absorbs (documented in `PayrollCalculator` remarks + submit handler §446-451 + tests). Either implement carry-forward or amend spec wording to "absorbed". Documented, tested, but spec-false as written.
2. **Spec text drift — 5110 vs 5130 (HR-02)**: spec scenarios + invariant name 5110; ledger posts 5130 (justified — 5110 is Office Supplies). Either add a spec amendment note or accept the seed-file rationale as canonical. All live/unit literals assert 5130.
3. **Spec status header stale**: `spec.md` header reads "IN PROGRESS — Block A implemented" while `tasks.md` claims "IMPLEMENTATION COMPLETE" and all 5 tasks verify. Header understates reality (inverse of the old false-CERTIFIED problem, but still inaccurate).
4. **Dead-from-HTTP handlers by design**: `CreateSalaryComponent` / `CreateSalaryStructure` / `AssignSalaryStructure` handlers are DI-registered and unit-tested but have NO POST routes (masters controller is reads-only; masters enter via SQL seeds, BOM precedent). Documented scope, but means the validated create paths are unreachable over HTTP — any future UI write flow must add routes + idempotency attributes.
5. **Replay coverage gap (HR-04)**: only submit replay is live-proven byte-identical with zero new rows. Disburse/cancel share the filter attribute but lack replay tests. Low risk (shared filter + `IdempotencyPolicyTests`), but a same-key disburse replay has never been executed.
6. **Intra-entry race proven only in fake (HR-06)**: live concurrency proof covers the cross-entry overlap shape; the same-(entry,employee) double-insert relies on the DB unique index + `LockEntrySlipsAsync` with only fake-level testing. Low risk (index exists in applied migration), but no live torn-insert execution.

### SUGGESTION
1. Add live replay tests for disburse and cancel with the same key (assert byte-identical + GL delta 0) to close WARNING 5.
2. Add a live test attempting a direct duplicate `(PayrollEntryId, EmployeeId)` slip insert (expect unique-violation → 409 `duplicate_salary_slip`) to close WARNING 6.
3. Amend `spec.md`: (a) HR-01 surplus wording → absorbed, (b) HR-02 account → 5130 with rationale pointer, (c) header status → match `tasks.md` reality.
4. The brief's expected RED `tsc` gate from `features/crm/` did not reproduce — `tsc --noEmit` exits 0 at this HEAD. No action; recorded so the next cycle doesn't chase a phantom.

## 7. Verdict

**PASS WITH WARNINGS**

Reasons: 5/5 tasks complete with executed proof; build 0/0; full suite 753/753 green (233 Domain + 448 Application + 72 Integration, incl. 5/5 live payroll lifecycle tests with exact-ledger oracles); frontend builds cleanly with hr-payroll chunks, `tsc` 0 errors, lint 0 errors / 0 hr-payroll warnings; DI complete for all hr-payroll handlers; idempotency attributes on all three posting mutations; RowVersion + frozen-period gates + slip locks all exercised. The six WARNINGs are documented drifts and coverage narrowings (absorb-vs-defer, 5130-vs-5110, stale spec header, seed-only masters, disburse/cancel replay untested, intra-entry race fake-only) — none is a failing test, untested scenario, or unchecked task, so none meets the CRITICAL bar. A green suite with these rows would still be PASS WITH WARNINGS, not FAIL: every invariant/scenario has at least a literal-level executed proof; the PARTIALs concern documented account-code/wording deltas and replay/race shapes adjacent to (not absent from) the proven paths.

### Compliance summary counts
- Tasks: **5/5 DONE** (12.1, 12.2, 12.3, 12.4, 12.5)
- Invariants/scenarios: **3 PASS** (HR-02-Phase-2, HR-03, HR-05), **4 PARTIAL with documented justification** (HR-01 absorb-drift, HR-02 5130-drift, HR-04 disburse/cancel-replay gap, HR-06 intra-entry live gap), **0 UNTESTED**, **0 FAIL**
- Issues: **0 CRITICAL / 6 WARNING / 4 SUGGESTION**
- Execution: build 0 errors; 753/753 tests; targeted 42 + 37 + 5 live green; vite clean; tsc 0 errors; lint 0 errors, 4 pre-existing foreign warnings
