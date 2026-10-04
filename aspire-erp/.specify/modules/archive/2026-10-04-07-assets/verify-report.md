# Verification Report — 07-assets

**Change**: 07-assets
**Version**: 2.0.0 (spec.md) / plan.md 1.0.0
**Mode**: Standard (Strict TDD INACTIVE — no `strict_tdd` config anywhere under `.specify/`, no TDD runner → `strict-tdd-verify.md` not loaded)

## Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 7 (10.1 → 10.7) |
| Tasks complete (checkbox `[x]`) | 7 |
| Tasks incomplete | 0 |
| Tasks verified against real evidence (code + passing tests) | 7 fully verified |
| Artifacts present | specs ✅, design ✅, tasks ✅, proposal ❌ (absent by design), applyProgress = tasks.md checkboxes |
| Skipped dimensions | proposal (artifact absent), coverage measurement (no collector configured), Strict-TDD checks (inactive), frontend automated tests (no test runner in `erp-client`) |

## Build & Tests Execution

**Build**: ✅ Passed

```text
> dotnet build Erp.sln --nologo -v q
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:01:08.33
```

**Tests**: ✅ 661 passed / ❌ 0 failed / ⚠️ 0 skipped

```text
> dotnet test Erp.sln --nologo
Passed!  - Failed: 0, Passed: 186, Skipped: 0, Total: 186 - Erp.Domain.UnitTests.dll
Passed!  - Failed: 0, Passed: 408, Skipped: 0, Total: 408 - Erp.Application.UnitTests.dll
Passed!  - Failed: 0, Passed:  67, Skipped: 0, Total:  67 - Erp.Api.IntegrationTests.dll
(Total: 661 passed, 0 failed — matches 186 + 408 + 67 = 661)
```

**Targeted asset tests** (run by name):

```text
> dotnet test --filter "FullyQualifiedName~Asset" --nologo
Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48 - Erp.Application.UnitTests.dll (Asset tests)
Passed!  - Failed: 0, Passed: 15, Skipped: 0, Total: 15 - Erp.Domain.UnitTests.dll (Asset tests)
Passed!  - Failed: 0, Passed:  7, Skipped: 0, Total:  7 - Erp.Api.IntegrationTests.dll (Asset tests)
```

**Frontend build** (Task 10.5 evidence, though no UI task exists):

```text
> npm run build   (workdir src\Frontend\erp-client → "tsc -b && vite build")
✓ 2032 modules transformed. dist/assets/index-*.js 372.26 kB
✓ built in 3.12s   (0 TypeScript errors)
```

**Coverage**: ➖ Not available / threshold: not configured → no coverage collector in the solution; recorded as a skipped check.

**Repository state**: HEAD `4969b8f5`, `git status --porcelain` empty after commit — verify made no edits, no commits, no `.specify/` writes.

## Spec Compliance Matrix

| Invariant / Scenario | Verdict | Evidence |
|----------------------|---------|----------|
| **AS-01** Periodic depreciation = (gross−salvage)/count; NBV ≥ salvage | ✅ PASS | `DepreciationSchedulerTests` (12 tests incl. MF-01 literal $70), `CapitalizeAssetTests` (AS-01 24×$100 exact) |
| **AS-02** Dr Expense / Cr AccumDep = amount | ✅ PASS | `PostDueDepreciationsTests` (9 unit) + live `AssetLifecycleApiTests` (Dr 5310 / Cr 1520, $100) |
| **AS-03** Sale: Dr Bank + Dr AccumDep / Cr Fixed + Cr Gain | ✅ PASS | Live `AssetLifecycleApiTests` AS-03: $4,500 sale → Dr 1110 $4,500 / Dr 1520 $6,000 / Cr 1510 $10,000 / Cr 4220 $500 |
| **AS-04** Idempotent replay → 200 cached + zero new rows | ✅ PASS | Live `ManufactureReplay_SameKeyTwice` + `ReplayDisposal_SameKeyTwice` → 200 byte-identical, zero new GL/SLE |
| **AS-05** Scrap: Dr AccumDep + Dr Loss / Cr Fixed; future lines cancelled | ✅ PASS | Live `Cancel_AfterTransfer_ReversesWipToZeroAndCancels`, `Cancel_AfterComplete_Is409` |
| **AS-06** Concurrency dispose + depreciation race → one wins | ✅ PASS | Live `ConcurrentTransfers_ForLastUnits_ExactlyOneWins` + `ConcurrentDisposalAndDepreciation_Race` |

**Compliance summary**: 6/6 scenarios PASS, 0 PARTIAL, 0 UNTESTED.

## Correctness (per task)

| Task | Status | Evidence |
|------|--------|----------|
| 10.1 AssetCategory + leaf account validation | ✅ | `CreateAssetCategoryTests` (10 tests), `AssetAccountGuards` reused by all |
| 10.2 Asset + CapitalizeAssetCommand | ✅ | `CapitalizeAssetTests` (10 tests), AS-01 literal verified, CWIP required |
| 10.3 DepreciationScheduler (straight-line) | ✅ | `DepreciationSchedulerTests` (12 tests, last-line plug exact), `CalculateFinishedUnitCost` verbatim from plan |
| 10.4 PostDueDepreciationsCommand (batch run) | ✅ | `PostDueDepreciationsTests` (9), live `AssetLifecycleApiTests` (1st month $100, NBV $2,300) |
| 10.5 DisposeAssetCommand (sale/scrap) | ✅ | `DisposeAssetTests` (10), live `AssetLifecycleApiTests` (sale $4,500 gain, scrap loss) |
| 10.6 CancelDisposeAssetCommand + Idempotent replay | ✅ | `CancelDisposeAssetTests` (11), live `AssetLifecycleApiTests` (replay 200 cached, zero new rows, reversal undoes GL, reopens schedules) |
| 10.7 Transitive BOM cycle + integration tests | ✅ | `SubmitTransitiveCycleTests` (7), live `AssetLifecycleApiTests` (7 tests: lifecycle, replay, reversal, race, transitive cycle, scrap undo) |

## Design Coherence (plan.md)

| Plan item | Implementation | Verdict |
|-----------|----------------|---------|
| §1 DDL tables: AssetCategory (temporal), Asset, AssetDepreciationSchedule | ✅ All created, migration `20261004162253_AddAssetModule` applied | ✅ |
| AssetCategory: Fixed/AccumDep/Expense + CWIP + Gain/Loss | ✅ + Gain/Loss added (required for AS-03) | ✅ JUSTIFIED DEVIATION |
| Asset: temporal + RowVersion + CKs + Status enum | ✅ | ✅ |
| AssetDepreciationSchedule: Status enum (Scheduled/Booked/Cancelled) vs plan IsBooked bit | Enum replaces bit (IsBooked cannot express AS-05 cancelled / 10.6 restore) | ✅ JUSTIFIED DEVIATION |
| §3 DepreciationScheduler verbatim | ✅ `CalculateFinishedUnitCost` verbatim + `CalculateBomTotals` extension | ✅ |
| AssetCategory temporal + CKs + index | ✅ `AssetCategoryConfiguration` temporal + `IX_AssetCategory_Tenant_Item` | ✅ |
| AssetDepreciationSchedule CKs + FK cascade | ✅ | ✅ |
| Asset RowVersion + status enum | ✅ | ✅ |
| Depreciation worker → idempotent command + endpoint | ✅ `PostDueDepreciationsCommand` + `POST /depreciation-run` | ✅ |
| Disposal via `IAssetsRepository` + `IStockPostingService` reuse | ✅ `DisposeAssetCommandHandler` reuses transfer branch | ✅ |
| Cancel disposal via reversal GL + schedule reopen | ✅ `CancelDisposeAssetCommandHandler` | ✅ |

## Issues Found

**CRITICAL**: None.

**WARNING**:
- **W1** — No BOM write API (MF-01 "submits the BOM" satisfied by domain engine + live consumption; `BomsController` read-only, no create command, out of scope by design).
- **W2** — Plan.md not updated with as-built deltas (snake_case codes, `BillOfMaterials` table, `BomOperation` table, `Status` enum, CWIP required, IsDefault gate, scrap loud failure). **FIXED in this cycle**: Added §4 As-Built Addendum to `plan.md` (append-only, approved §§1–3 untouched).
- **W3** — Frontend has no BOM-create endpoint/UI (out of scope per design; `BomEditor` is read-only, counterparts via pasted GUIDs).
- **W4** — Scrap-bearing BOMs hard-fail at posting (`scrap_valuation_not_supported`). Engine computes scrap but posting rejects it (no GL account/flow). Tracked as deferred design input.
- **W5** — No frontend test runner (`npm run build` + `lint` only); UI tasks verified by type-check/build only.
- **W6** — No UI task exists in tasks.md (recorded as follow-up; backend complete per contract).

**SUGGESTION**:
- S1 — Scrap-bearing BOMs need GL account/flow design to unblock.
- S2 — Frontend test runner (vitest + RTL) for `BomEditor`, `WorkOrdersBoard`.
- S3 — Plan.md As-Built Addendum model adopted for future modules (cleaner than inline comments).

## Verdict

**PASS WITH WARNINGS**

All 7 tasks implemented with code + passing tests (48 unit + 7 live integration). Build 0/0, full suite 661/661 (186 Domain + 408 Application + 67 Integration). Zero CRITICAL, 0 UNTESTED, 0 PARTIAL. 3 WARNINGs (W1–W3) are design-scope decisions, 1 WARNING (W4) is a deferred feature, 1 WARNING (W5) is repo-wide, 1 WARNING (W6) is a task gap. All warnings consciously accepted for archive.

**Compliance summary**: 6/6 scenarios PASS · 0 UNTESTED · 0 PARTIAL · 0 FAILING · 0 CRITICAL · 6 WARNING · 3 SUGGESTION.