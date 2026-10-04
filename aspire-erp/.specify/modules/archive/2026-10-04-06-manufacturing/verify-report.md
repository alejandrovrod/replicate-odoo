# VERIFICATION REPORT — Change `06-manufacturing` (Manufacturing & Production)

**Verifier role:** VERIFY EXECUTOR (read-only; no fixes applied). HEAD: `9997cb9a` ("docs(sdd): tick 06-manufacturing tasks 9.5-9.7, implementation complete"). Working tree clean (`git status --short` empty). Spec header at verification time reads "IN PROGRESS — Block A implemented (tasks 9.1, 9.2); Block B/C pending" / v2.0.0 — i.e. the spec status line itself is now STALE (implementation is complete per tasks.md + code + tests); tasks.md reads "IMPLEMENTATION COMPLETE — all 7 tasks verified (pending Spec Kit verify + archive)".

---

## 1. Completeness (7/7 tasks vs real evidence)

| Task | Claimed | Code evidence | Test evidence | Result |
|------|---------|---------------|---------------|--------|
| 9.1 Workstation & Machine Center | [x] | `Erp.Domain.Entities.Workstation` (Labor/Electricity/Rent + computed `HourRateTotal` getter; private setter solely for EF computed-column materialization) + `WorkstationValidator`; `Workstation` table w/ computed `HourRateTotal` in `20261004132811_AddManufacturingModule` | `WorkstationTests` (Domain, in the 68-test targeted Domain re-run, green) | ✅ VERIFIED |
| 9.2 BOM aggregate & cost roll-up | [x] | `BillOfMaterials` + `BomItem` + `BomOperation` + `BomValidator` (quantity/has-items/self-ref/cycle/item/operation guards) + `ManufacturingCostEngine.CalculateBomTotals` (raw+operating−scrap, 4dp) + `CalculateFinishedUnitCost` (verbatim plan §3 + hardening branches) | `ManufacturingCostEngineTests` (MF-01 literal $70 ×2 tests, scrap, guards) + `BomValidatorTests` (in 68-test Domain re-run, green) | ✅ VERIFIED |
| 9.3 Work Order scheduling & reservation | [x] | `WorkOrder` aggregate (Draft→Submitted→InProcess→Completed, Cancelled from non-terminal; gapless `WO-YYYY-NNNNN` via `NextWorkOrderNumberAsync` in `CreateWorkOrderCommandHandler`; `RowVersion` rowversion in `WorkOrderConfiguration`) + `CreateWorkOrderCommandHandler` + `SubmitWorkOrderCommandHandler` (IsActive + IsDefault gates + transitive-cycle DFS) | `WorkOrderTests` (transitions) + `WorkOrderWorkflowTests` (`Submit_InactiveBom_FailsAndLeavesDraft` → `inactive_bom`, `Submit_NonDefaultBom_FailsAndLeavesDraft` → `non_default_bom`, in 46-test Application re-run, green) | ✅ VERIFIED |
| 9.4 WIP & Finish stock movements | [x] | `TransferMaterialsToWipCommandHandler` (Stores→WIP via `IStockPostingService.PostAsync`, Submitted→InProcess + `TransferStockEntryId` link, same ambient transaction) + `ManufacturingPostingService.CompleteAsync` (frozen-period gate first, UPDLOCK/HOLDLOCK range locks, FIFO pass-1 consume-all-before-write, 3-leg GL Dr1330/Cr1320/Cr5210, `DoubleEntryGuard`, gapless MF voucher, InProcess→Completed) | `TransferMaterialsToWipTests` + `CompleteManufactureTests` (46-test re-run green) + live `FullCycle_TransferThenManufacture_PostsExactPairsAndCompletes` (7-test live re-run green) | ✅ VERIFIED |
| 9.5 React Manufacturing & BOM Studio UI | [x] | `ManufacturingOverview.tsx` (live status counts via `/v1/workorders`), `WorkOrdersBoard.tsx` (submit/transfer/complete/cancel flow), `BomEditor.tsx`, `useManufacturingData.ts` | `npm run build` ✅ (manufacturing chunks emitted: `BomEditor`, `WorkOrdersBoard`, `useManufacturingData`; 0 errors) + `npm run lint` 0 errors (3 pre-existing warnings in banking/buying files, none in manufacturing). No test runner in repo — recorded as skipped dimension, not a failure. | ✅ VERIFIED (build+lint; tests N/A by repo standard) |
| 9.6 Idempotent manufacture & reversal handlers | [x] | `[IdempotencyKeyRequired]` on transfer/complete/cancel routes (replay rides the shared idempotency pipeline, no new backend code) + `CancelWorkOrderCommandHandler` (Submitted/InProcess-only, ProducedQuantity==0, compensating WIP→Stores MT via `IStockPostingService`, `TransferStockEntryId` option-(a) link, one ambient transaction) | `CancelWorkOrderTests` (46-test re-run green) + live `ManufactureReplay_SameKeyTwice_SecondIs200ByteIdenticalWithZeroNewRows`, `Cancel_AfterTransfer_ReversesWipToZeroAndCancels`, `Cancel_AfterComplete_Is409WithZeroNewRows` (7-test live re-run green) | ✅ VERIFIED |
| 9.7 BOM recursion & cost roll-up tests | [x] | Transitive closure DFS in `SubmitWorkOrderCommandHandler.EnsureNoTransitiveCycleAsync` (iterative stack, path-scoped visited set, default-active-BOMs only) over `BomValidator.EnsureNoCycle` plug-in point | `SubmitTransitiveCycleTests` (7 unit: A→B→A, 3-level, self-ref, diamond-pass, acyclic-pass, inactive-skip, non-default-skip) + live `Submit_TransitiveCycleChain_RejectsWithCircularReferenceAndZeroWrites` + `Submit_DiamondSharedSubComponent_SubmitsSuccessfully` + ΣDebit==ΣCredit asserted on every live posting (`totalDebit==totalCredit` + per-account nets) | ✅ VERIFIED |

**Completeness: 7/7 with code + passing tests each. No unchecked task.**

---

## 2. Build & Tests Execution (run at HEAD by this verifier; nothing else running)

### `dotnet build Erp.sln --nologo -v q`
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:01:02.91
```
No MSB3021/MSB3027 file-lock errors encountered; no retry needed; no stale process to report.

### `dotnet test Erp.sln --nologo --no-build` (full suite)
```
Passed!  - Failed: 0, Passed: 174, Skipped: 0, Total: 174, Duration: 356 ms - Erp.Domain.UnitTests.dll (net10.0)
Passed!  - Failed: 0, Passed: 357, Skipped: 0, Total: 357, Duration: 1 s - Erp.Application.UnitTests.dll (net10.0)
Passed!  - Failed: 0, Passed:  67, Skipped: 0, Total:  67, Duration: 48 s - Erp.Api.IntegrationTests.dll (net10.0)
```
**598 total (174 + 357 + 67), 0 failed, 0 skipped** — matches the orchestrator's last-run count exactly (357/174/67). No regressions.

### Targeted re-runs by name
- Domain (`WorkstationTests`, `ManufacturingCostEngineTests`, `BomValidatorTests`, `WorkOrderTests`): `Passed: 68, Failed: 0`
- Application (`WorkOrderWorkflowTests`, `TransferMaterialsToWipTests`, `CompleteManufactureTests`, `CancelWorkOrderTests`, `SubmitTransitiveCycleTests`): `Passed: 46, Failed: 0`
- Live (`WorkOrderManufacturingApiTests`, dev DB `erp-db` on 127.0.0.1:1433): `Passed: 7, Failed: 0, Duration: 15 s`

### Frontend (`src/Frontend/erp-client`)
- `npm run build` (`tsc -b && vite build`): ✅ built in 2.40s, 2032 modules, manufacturing chunks emitted (`BomEditor-*.js 7.50 kB`, `WorkOrdersBoard-*.js 7.52 kB`, `useManufacturingData-*.js 1.54 kB`), 0 errors.
- `npm run lint` (`oxlint`): **0 errors**, 3 warnings — all pre-existing in `banking/BankReconciliation.tsx` and `buying/BuyingOverview.tsx` (set-state-in-effect / exhaustive-deps); **zero warnings in manufacturing files**.
- Test runner: none in repo — skipped dimension (repo standard, same as prior modules).

---

## 3. Spec Compliance Matrix

### Invariants
| Invariant | Verdict | Named test(s) |
|-----------|---------|---------------|
| MF-01 capitalization formula `(ΣRaw + ΣOperating − Scrap)/ProducedQty` | ✅ PASS | `ManufacturingCostEngineTests.CalculateFinishedUnitCost_Mf01Literal_ReturnsSeventy` ($70), `…_ScrapValue_DeductsFromNetCost` ($65), `CalculateBomTotals_Mf01Literal_ReturnsExactSnapshots` (50/20/0/70), plus hardening (zero/negative qty throws, scrap %, empty inputs). Engine is verbatim plan §3 `CalculateFinishedUnitCost` + documented `CalculateBomTotals` extension. |
| MF-02 transfer Dr1320/Cr1310; manufacture Dr1330/Cr1320/Cr5210, Σ=0 | ✅ PASS | Live `FullCycle_…` asserts transfer GL (Dr1320 500 / Cr1310 500 via `AssertTransferGl`), manufacture triple (Dr1330 700 / Cr1320 500 / Cr5210 200 via `Sum(gl,…)`), `totalDebit==totalCredit`, per-account nets (Stores 0, WIP 0, FG +700, absorption −200), exact row deltas (7 SLE + 7 GL). Balance also guarded pre-save by `DoubleEntryGuard.EnsureBalanced` in the service. |
| MF-03 active-BOM guard (no WO without valid active BOM) | ✅ PASS | `WorkOrderWorkflowTests.Submit_InactiveBom_FailsAndLeavesDraft` (`inactive_bom`, stays Draft) + `Submit_NonDefaultBom_FailsAndLeavesDraft` (`non_default_bom`) + transitive-cycle rejections leave Draft with zero writes (live + unit). Note: spec text says "active" only; implementation additionally requires IsDefault — documented as Task 9.3 acceptance, tested, justified (see Design Coherence). |

### Scenarios (adversarial literals)
| Scenario | Verdict | Evidence — exact gap or exact proof |
|----------|---------|--------------------------------------|
| MF-01 $70 BOM (2×A@$15 + 1×B@$20 + 30min WS-01@$40/h, IsActive + IsDefault) | ✅ PASS | Domain literals: `Mf01Items` = 2×15 + 1×20, `Mf01Operations` = 30min@$40 → raw 50 / operating 20 / scrap 0 / total 70 asserted to the cent (`CalculateBomTotals_Mf01Literal_ReturnsExactSnapshots`, `…_UnitCost_Mf01Literal_ReturnsSeventy`). IsActive/IsDefault asserted at submit (`Submit_InactiveBom…`, `Submit_NonDefaultBom…`). WS-01 $40/h = 25+10+5 seeded in `seed-dev-manufacturing.sql` and consumed live (30min → $20 operating inside the $200/10-unit scale). |
| MF-02 transfer $500 pair live | ✅ PASS | Live `FullCycle_…`: 20×A + 10×B Stores→WIP, `AssertTransferGl(debit1320: 500, credit1310: 500)` on the response body + WIP on-hand assertions + delta (4 SLE + 4 GL for the transfer leg of the 7+7 total). |
| MF-03 $700 FG @ $70, $500/$200 split, Completed live | ✅ PASS | Live `FullCycle_…`: FG line qty 10 @ rate $70; GL `Sum(1330,debit)==700`, `Sum(1320,credit)==500`, `Sum(5210,credit)==200`; `totalDebit==totalCredit`; status `Completed`; FG on-hand 10; per-account oracles (Stores 0 / WIP 0 / FG +700 / absorption −200 across all four run vouchers). |
| MF-04 idempotent replay → 200 cached, zero duplicates | ✅ PASS | Live `ManufactureReplay_SameKeyTwice_…`: first 201, replay same key → **200 with byte-identical body** (`Assert.Equal(firstBytes, replayBytes)`), **zero new SLE/GLEntry rows** (count deltas), order `Completed`, FG on-hand 1. Rides the shared idempotency pipeline (no new backend code — the test is the proof the guard holds on this route). |
| MF-05 cancel → compensating WIP→Stores + GL mirror, WIP zero | ✅ PASS | Live `Cancel_AfterTransfer_…`: NEW compensating MT voucher WIP→Stores (4 SLE + 4 GL, header verified `MaterialTransfer` via SQL), WIP on-hand back to 0 for both items, transfer+reversal per-account nets 0/0, status `Cancelled`; plus `Cancel_AfterComplete_Is409WithZeroNewRows` (`invalid_status_transition`, zero new rows, stays `Completed`, FIFO never unpicked). |
| MF-06 concurrent issues → one wins, loser InsufficientStockException | ✅ PASS (with noted mapping) | Live `ConcurrentTransfers_ForLastUnits_ExactlyOneWins`: truly concurrent `Task.Run` pair on the last 20 units → exactly one 201, loser **400 `insufficient_stock`** (the API's RFC7807 mapping of the domain `InsufficientStockException`; handler catches it and returns failure, controller maps to 400 — verified in `TransferMaterialsToWipCommandHandler` catch + `ManufacturingProblem`). Stores drained exactly to 0, never negative (range-lock serialization). Winner cancelled + provisioned stock issued back out (neutral footprint). **No live RowVersion-concurrency test on the WO transition itself** — RowVersion is configured (`IsRowVersion`) and the concurrency catch paths exist in all three handlers, but MF-06's observable proof is the stock-race test, which is the spec's actual scenario. Recorded as observation, not a gap. |

**Untested/partial rows: none. All 3 invariants + 6 scenarios have named passing tests with exact-figure oracles.**

---

## 4. Correctness (per task)

- **9.1:** Composite rate is a live getter (`HourRateLabor+Electricity+Rent`), mirroring the DDL computed column; EF private setter is documented as materialization-only and discards (getter recomputes — no staleness). Validators reject negative rates. ✅
- **9.2:** Roll-up rounds each line to 4dp (AwayFromZero), then sums, then totals — matches GL scale and FIFO precedent. Scrap flows into `ScrapCost` snapshot and deducts (`total = raw + operating − scrap`). Anti-cycle: direct self-ref (`EnsureNoSelfReference`) + transitive path-scoped DFS (`EnsureNoCycle` + submit-time walk). Scrap >100% stays legal (ERPNext multi-output) — documented. ✅
- **9.3:** Submit gate order is correct (existence → IsActive → IsDefault → cycle → transition; rejections write zero rows). Gapless WO numbering inside the creation transaction (purchase-order precedent). Three-warehouse routing persisted with Restrict deletes. ✅
- **9.4:** Transfer reuses the stock engine (no duplicated posting logic); completion engine ordering is correct (validate → frozen-gate FIRST → locks → pass-1 consume-all → amounts → GL → `DoubleEntryGuard` → voucher → save → transition, all in one ambient transaction). Operating cost is planned-operations basis scaled by produced qty (JobCard actuals explicitly deferred — documented). Absorption leg resolves via `Company.CogsAccountCode` (code-not-FK, decision D3) → 5210 in dev seed. ✅
- **9.5:** UI reads live endpoints only (`GET /v1/workorders`, BOM reads); execution board drives the real workflow mutations. ✅
- **9.6:** Idempotency rides the shared pipeline on all three ledger-posting mutations; cancel linkage is option (a) (`TransferStockEntryId`, single-transfer-per-order by design, partial staging deferred). Draft cancel is deliberately rejected (workflow no-op by design) — deviation from a naive MF-05 reading, documented in the handler. ✅
- **9.7:** Cycle walk follows default-active BOMs only (inactive/non-default recipes skipped — proven by unit tests); diamonds pass (path-scoped visited set); iterative stack (no recursion overflow). ✅

---

## 5. Design Coherence (plan DDL vs implementation)

| Plan item | Implementation | Judgment |
|-----------|---------------|----------|
| §1 table `Workstation` with computed `HourRateTotal` | `Workstation` table, computed column (type `decimal(18,2)` vs plan's implied 18,4 — cosmetic; domain getter is exact decimal) + `IX_Workstation_Tenant_Company_Name` | ✅ JUSTIFIED (superset: added lookup index) |
| §1 table `BOM` temporal + `IX_BOM_Tenant_Item` | Table named **`BillOfMaterials`** (+ `BillOfMaterialsHistory`), `IX_BOM_Tenant_Item` present, FK names `FK_BOM_Item`/`FK_BOM_UOM` kept | ✅ JUSTIFIED (EF pluralization convention; constraint/index names preserve plan identity; temporal history proven live via `FOR SYSTEM_TIME ALL` assertion) |
| §1 table `BOMItem` | `BomItem` table with plan CHECKs + cascade to BOM | ✅ VERBATIM |
| §1 table `WorkOrder` + RowVersion + `IX_WorkOrder_Tenant_Status` | `WorkOrder` table with `rowversion`, int status, both plan indexes + gapless-number index | ✅ VERBATIM (+ justified extra voucher index) |
| Plan "NO BOMOperation/JobCard tables" (task brief §1 lists 4 tables) | **`BomOperation` table ADDED** (FK→BOM cascade, FK→Workstation restrict, duration CHECK) — required by tasks.md 9.2 ("Implement BOM, BOMItem, and BOMOperation") and spec operations costing; no JobCard table (actuals deferred) | ✅ JUSTIFIED (task directive overrides the abbreviated DDL list; operations are load-bearing for the $200 MF-03 leg) |
| §2 error catalog SCREAMING_SNAKE `MFG_*` in `Erp.Domain.Manufacturing.Errors` | snake_case codes in `Erp.Domain.Entities` (`inactive_bom`, `circular_reference`, …) — deviation explicitly documented in the file's `<remarks>` | ✅ JUSTIFIED (repo-wide convention; every controller mapping + test asserts the snake_case codes; consistency beats plan literal) |
| §3 `ManufacturingCostEngine` verbatim + hardening | `CalculateFinishedUnitCost` verbatim (incl. `ArgumentOutOfRangeException` message); `CalculateBomTotals` added with per-line guards | ✅ VERBATIM + justified extension |
| Enum persistence (int), temporal history, gapless WO/MF numbering | int conversion (purchase-order precedent), SYSTEM_VERSIONING ON, WO `NextWorkOrderNumberAsync` / MF `NextVoucherNumberAsync` | ✅ as planned |
| DI: all manufacturing handlers + queries registered | `Program.cs` 182–191: repository, posting service, 5 command handlers, 3 query handlers + `IdempotencyFilter` | ✅ COMPLETE — audited all 8 manufacturing handler registrations; no dead route (prior-cycle failure mode absent). `GetBoms/GetBomDetail/GetWorkOrders` query handlers registered (prior "dead DI route" class checked). |
| `[IdempotencyKeyRequired]` on posting mutations | Present on transfer (162), complete (210), cancel (258); absent on create/submit/list (correct — no SLE/GLEntry writes, purchase-order precedent) | ✅ CORRECT SCOPE |
| Frozen-period gates on posting dates | `company.EnsurePostingDateUnlocked` in `ManufacturingPostingService` (first, before any row built) + transfer/cancel paths via `IStockPostingService` + `FiscalPeriodLockedException`→409 mapping | ✅ PRESENT on all posting paths |
| Range locks on WIP consume paths | `LockStockRangeAsync` over WIP+target Kardex rows before FIFO reads in completion; transfer path inherits stock-engine locks | ✅ PRESENT |
| Migration `20261004132811_AddManufacturingModule` | Creates all 5 tables + indexes + checks; Down drops in dependency order; applied (live tests post against these tables successfully) | ✅ APPLIED & CONSISTENT with model (live green = runtime EF-model valid — prior "runtime EF-model break" class checked) |
| Seeds (`seed-dev-manufacturing.sql`) | Idempotent guarded inserts: 1320/1330 leaves, WIP-01→1320, WS-01 25/10/5=$40; never lists generated/temporal/computed columns | ✅ IDEMPOTENT & CORRECT |
| No BOM-CRUD write endpoints | `BomsController` is read-only (list + detail); BOM masters enter via SQL seeds (dev script + per-test `InsertBomAsync`). | ⚠️ NOTED (see WARNING W2 — spec MF-01's "submits the BOM" is proven at the domain-engine level, not via an endpoint; out-of-scope by design) |
| Scrap-bearing manufacture | Loud `scrap_valuation_not_supported` rejection (no invented scrap GL policy) | ✅ JUSTIFIED loud failure (see SUGGESTION S1 for the deferred design) |

---

## 6. Issues Found

### CRITICAL — none.
No failing tests, no untested spec row, no unchecked task, no unregistered handler, no missing idempotency guard, no absent frozen-gate or lock. Full suite green (598/598).

### WARNING
- **W1 — Stale spec status header.** `spec.md` header still reads "IN PROGRESS — Block A implemented (tasks 9.1, 9.2); Block B/C pending" while all 7 tasks are implemented and verified. This is exactly the "false header" failure mode from prior cycles (previously a false "100% CERTIFIED"; now a false "IN PROGRESS"). The header must be updated to IMPLEMENTATION COMPLETE / verified-pending-archive before archive. (Tasks.md already carries the correct status.)
- **W2 — No BOM write path (MF-01 "submits the BOM" has no endpoint).** `BomsController` exposes list + detail only; BOM masters enter via SQL seeds (dev script + per-test `InsertBomAsync`). The $70 costing itself is proven at the domain-engine level (two literal tests) and consumed live, so the accounting invariant is verified — but the *user-facing* "engineer submits the BOM" flow from MF-01 has no API surface and therefore no negative-path coverage (e.g. submitting an empty/cyclic BOM through an endpoint). Accepted as designed scope (controller documents it), but the spec scenario's "When the engineer submits the BOM" step is satisfied by construction, not by execution. Flagging so archive consciously accepts it.
- **W3 — Documented plan deviations live only in code comments.** snake_case error codes, `BillOfMaterials` naming, `BomOperation` table addition, IsDefault gate beyond spec text, Draft-cancel rejection, and scrap-loud-failure are each justified in situ, but plan.md itself was not updated to v1.1.0 recording the as-built deltas. Recommend a plan addendum at archive so the next reader doesn't re-litigate them.
- **W4 — MF-06 concurrency proof is stock-level, not RowVersion-level.** The live race proves serialization + starvation via Kardex range locks (the spec's actual scenario). `RowVersion` (`IsRowVersion`) and the `ConcurrencyConflictException`→409 paths in all three handlers have no dedicated concurrent-transition test (two submitters racing Draft→Submitted). Low risk (standard EF mechanism, catch paths mirror tested modules), but the token itself is unexercised under contention.

### SUGGESTION
- **S1 — Scrap-bearing manufacture is a hard wall.** Any BOM with `ScrapCost ≠ 0` fails completion with `scrap_valuation_not_supported` — including penny-rounding scrap from `ScrapPercentage` lines (the engine computes scrap, the posting rejects it). Correct conservative choice (no invented GL policy), but it means the scrap-deduction path proven in `CalculateBomTotals_ScrapPercentage_DeductsSalvageValue` can never execute end-to-end until a scrap GL account/flow is designed. Track as deferred design input, not a defect.
- **S2 — `ReadNewEntryIdAsync` (cancel test) changes "TOP 1 … ORDER BY CreatedAt DESC" excluding the transfer id.** Sound under the serialized `LedgerMutatingCollection`, but a wall-clock tie with an unrelated test's voucher could theoretically misattribute. The subsequent entry-type + per-account-net assertions would still catch a wrong pick (nets would not zero), so this is robustness commentary only.
- **S3 — Lint warnings are non-manufacturing but real.** The 3 `oxlint` warnings (banking/buying `set-state-in-effect` / `exhaustive-deps`) are outside this module; consider a follow-up cleanup so future `npm run lint` output is warning-free and module verdicts stay unambiguous.

---

## 7. Verdict: **PASS WITH WARNINGS**

All 7 tasks are implemented with code + passing-test evidence; build is 0/0; the full suite is green at 598/598 with zero regressions; every invariant and scenario maps to named passing tests with exact-figure oracles (including $70/$500/$700/$500/$200 literals, byte-identical 200 replay with zero new rows, compensating voucher with WIP-zero and GL-net-zero, and the exact winner/loser race with no negatives); DI is complete; idempotency/frozen-period/locks are present on all posting paths; the migration is applied and runtime-valid (live tests green). The four WARNINGs (stale spec header, no BOM write path, undocumented-as-built plan deltas, unexercised RowVersion contention) do not fail any spec row but must be consciously accepted — W1 and W3 — at archive.

### Compliance summary counts
- Tasks: **7/7 verified** (code + passing tests each).
- Invariants: **3/3 PASS** (MF-01, MF-02, MF-03).
- Scenarios: **6/6 PASS** (MF-01…MF-06), **0 UNTESTED, 0 PARTIAL**.
- Build: **0 warnings / 0 errors**. Full suite: **598 passed / 0 failed / 0 skipped** (174 Domain + 357 Application + 67 Integration). Targeted: Domain 68/68, Application 46/46, live manufacturing 7/7. Frontend: build ✅ 0 errors (manufacturing chunks emitted), lint 0 errors / 0 manufacturing warnings (3 pre-existing elsewhere), tests skipped (no runner — repo standard).
- Cross-cutting: DI 8/8 handlers+queries registered; `[IdempotencyKeyRequired]` 3/3 posting mutations (create/submit/list correctly exempt); RowVersion configured + mapped (contention untested — W4); frozen gates + range locks present on all posting paths; no regressions.
- Issues: **0 CRITICAL / 4 WARNING / 3 SUGGESTION.**
