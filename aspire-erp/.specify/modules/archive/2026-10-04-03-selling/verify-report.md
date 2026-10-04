# Verification Report — 03-selling

**Change**: 03-selling (RE-VERIFICATION round 2 — after scope amendment `1a17c866`)
**Version**: 2.0.0 (spec.md, amended 2026-10-04 — status line + inline DEFERRED markers)
**Mode**: Standard (Strict TDD INACTIVE — no `strict_tdd` config in `.specify/`, no TDD runner → `strict-tdd-verify.md` not loaded)
**Code state**: HEAD `1a17c866` — source identical to the state covered by the orchestrator's full-suite run (see git corroboration below)

## Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 6 (5.1, 5.2, 5.2b, 5.3, 5.4, 5.5) |
| Tasks complete (checkbox `[x]`) | 6 |
| Tasks incomplete | 0 |
| Tasks verified against real evidence (code + passing tests) | 3 fully verified in scope (5.1, 5.2, 5.2b); 3 checked `[x]` but their subject matter is now DEFERRED by the spec amendment (5.3 sales-invoice posting, 5.4 POS checkout, 5.5 POS cashier UI) and `modules/03-selling/tasks.md` was **not** annotated (W11) |
| Artifacts present | spec ✅ (amended, `git show --stat 1a17c866` → 1 file, `spec.md` only), design ✅ (`plan.md`, unchanged), tasks ✅ (unchanged, un-annotated), proposal ❌ (missing by design), applyProgress = tasks.md checkboxes |
| Scope basis for this round | 6 of 10 spec rows carry inline `DEFERRED — scope amendment 2026-10-04 (retro-verify)` markers (`spec.md:31,44,47,71,80,94,103,112`); those rows are **excluded from compliance scoring**; 4 rows remain in scope |
| Skipped dimensions | proposal (artifact absent), coverage measurement (no collector configured), Strict-TDD checks (inactive), frontend automated tests (no test runner in `erp-client`), full integration assembly (not run by this verifier — concurrent verifier active; orchestrator evidence cited instead) |

**Amendment integrity check (performed this round):** every factual claim inside the new DEFERRED markers was independently re-verified against the source at `1a17c866` and is accurate — unrouted invoice handlers + `TaxTotal = 0` (`CreateSalesInvoiceCommandHandler.cs:76`, `Program.cs:117-118`, no route anywhere), unseeded POS GUIDs / tax-inclusive tender mismatch (`PosCashierModal.tsx:55-56,62-63,73` vs `SubmitPOSInvoiceCommandHandler.cs:105,134-135`), no COGS/stock pair (`:141-156`), no `[IdempotencyKeyRequired]` on `POST .../pos` (`SalesInvoicesController.cs:19-31`), no `CancelSalesInvoice*` anywhere, `SalesInvoiceStatus.Cancelled` never assigned, `RowVersion` conflict mapped to `server_error` (`SubmitSalesInvoiceCommandHandler.cs:140-142`), and the missing stock range lock (`spec.md:112`). The amendment does not paper over anything false.

## Build & Tests Execution

**Build (this verifier, at HEAD `1a17c866`)**: ✅ Passed — 0 warnings / 0 errors

```text
> dotnet build Erp.sln --nologo -v q   (workdir C:\Workspace\Odoo\aspire-erp)
# attempts 1-2 failed with environment file-lock errors only, NOT code errors:
#   CS2012  Erp.ServiceDefaults.dll ... used by another process ('Microsoft Defender Antivirus Service' (6040))
#   MSB3491 Erp.Api\obj\...\AssemblyInfoInputs.cache ... file already exists   (concurrent build contention)
attempt 3:
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:34.46
```

**Full suite (orchestrator run at this exact source state — cited, not re-run by this verifier)**: ✅ 370/370 passed

```text
370 total, 0 failed — 86 Erp.Domain.UnitTests + 231 Erp.Application.UnitTests + 53 Erp.Api.IntegrationTests; build 0 Warning(s) / 0 Error(s)
```

**Git corroboration of "no source change since that run":**

```text
> git rev-parse --short HEAD  → 1a17c866 ; git status --porcelain → empty (clean tree)
> git log --oneline -6
1a17c866 docs(sdd): amend 03-selling spec to mark deferred scope      (.specify/modules/03-selling/spec.md only — 1 file)
0fa7ee1a docs(sdd): amend 01-accounting spec to mark deferred scope    (.specify only — 1 file)
e599ecac docs(sdd): archive 02-stock module and promote spec           (.specify only — 9 files)
99f5d518 docs(sdd): repair 04-buying index links after archive         (.specify/plan.md, spec.md, tasks.md only)
2460d84e fix(stock): wire stock entry cancellation endpoint ...        (LAST source commit: src/.../Program.cs + 2 test files)
55553ce5 test(stock): add adversarial concurrency test ...
```
All four commits after the last source-touching commit (`2460d84e`) modify only `.specify/` documents → HEAD's source tree is byte-identical to the state the orchestrator ran; corroborated independently by my green build and narrow test runs below.

**Verifier's narrow unit re-runs (module-scoped, `--no-build`, no DB)**: ✅ 49/49 green

```text
> dotnet test tests/Erp.Application.UnitTests --filter "...~SalesPostingServiceTests|...~SalesOrderWorkflowTests|...~CreateCustomerCommandHandlerTests"
Passed!  - Failed: 0, Passed: 41, Skipped: 0, Total: 41, Duration: 271 ms
> dotnet test tests/Erp.Domain.UnitTests --filter "...~CreditControlEvaluatorTests"
Passed!  - Failed: 0, Passed:  8, Skipped: 0, Total: 8, Duration: 50 ms
```

**Citation re-check**: all test methods cited in the matrix below re-grepped at HEAD and confirmed to exist (`SalesPostingServiceTests.cs:178,483`, `SalesOrderWorkflowTests.cs:239`, `SalesOrderDeliveryNoteApiTests.cs:169,229,367,494`, `CreditControlEvaluatorTests.cs:45`, `CreateCustomerCommandHandlerTests.cs:24`); no `SalesInvoice`/`POS` test exists in `tests/` (unchanged).

**Coverage**: ➖ Not available / threshold: not configured → recorded as a skipped check.

**Repository state**: HEAD `1a17c866`, `git status --porcelain` empty before and after — verify made no file writes, no commits, no `.specify/` edits, no Engram writes.

## Spec Compliance Matrix

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| Invariant SL-01 (Dr A/R = GrandTotal, Cr Revenue = NetTotal, Cr Taxes Payable = TaxTotal, ΣDr−ΣCr = 0.0000) | sales-invoice posting equation | Deferred — `spec.md:31` (unrouted handlers, `TaxTotal = 0`, no GL test) | ⏭️ OUT OF SCOPE (DEFERRED) — excluded from scoring |
| Invariant SL-02 (`OutstandingDebt + GrandTotal ≤ CreditLimit`, else `CreditLimitExceededException`) — **SalesOrder path** | credit exposure gate | `CreditControlEvaluatorTests` (8: `ValidateCreditExposure_ExposureOverLimit_ThrowsCreditLimitExceeded:45`, `_ExactlyAtLimit_Allows:62`, `_BypassCreditLimitCheckSet_NeverThrows:75`, `_ZeroLimit_NeverThrows:87`, `_OutstandingAmountParticipatesInExposure:98`, …) + `SalesOrderWorkflowTests > Submit_BreachedCreditLimit_FailsWithCreditLimitExceededAndOrderStaysDraft:239` + `SalesOrderDeliveryNoteApiTests > SubmitSalesOrder_WhenCreditLimitBreached_Returns409AndOrderStaysDraft:169` | ✅ COMPLIANT — `credit SalesInvoice` clause excluded per `spec.md:44` (was W4, now deferred) |
| Invariant SL-03 (Atomic POS checkout) | POS | Deferred — `spec.md:47` | ⏭️ OUT OF SCOPE (DEFERRED) — excluded from scoring |
| Invariant SL-04 (`DeliveredQty ≤ Quantity − DeliveredQty`) | non-overdelivery guard | `SalesPostingServiceTests > PostDeliveryNoteAsync_Overdelivery_FailsWithOverdeliveryNotAllowedBeforeAnyWrite:304`, `_OverdeliveryOnPartiallyDeliveredLine_UsesRemainingQuantity:327`, `_ForeignOrderLine_…:343`, `_LineItemMismatch_…:357` + `SalesOrderDeliveryNoteApiTests > PostDeliveryNote_ExceedingRemainingQuantity_Returns400WithOverdeliveryNotAllowed:367`, `_ForDraftOrder_Returns409WithSalesOrderNotDeliverable:428` | ✅ COMPLIANT — guard runs in phase 1 before any FIFO read (`SalesPostingService.cs:114-158`) |
| Scenario SL-01 (order-to-cash) — **DeliveryNote half certified** | relief from warehouse + COGS booked | `SalesPostingServiceTests > PostDeliveryNoteAsync_SingleLine_WritesBalancedCogsAndKardexRows:178`, `DeliveryPostings_SatisfyDoubleEntryZeroSumInvariant:483`, `PostDeliveryNoteAsync_SecondDelivery_GetsNextGaplessVoucher:236` + `SalesOrderDeliveryNoteApiTests > PostDeliveryNote_FullQuantity_Returns201BalancedGlAndCompletesOrder:229` (Dr 5210 / Cr 1310, `totalDebit == totalCredit`, Kardex relief, `Completed` @ 100%), `_PartialThenRemainingDelivery_TransitionsPartiallyDeliveredThenCompleted:324` | ✅ COMPLIANT (certified half) — `SalesInvoice` half DEFERRED per `spec.md:71`, excluded from scoring |
| Scenario SL-02 (breach → `CreditLimitExceededException("Credit limit $5,000 exceeded. Current: $4,600, Attempted: $650")`) | credit-limit rejection | `CreditControlEvaluatorTests > ValidateCreditExposure_ExposureOverLimit_ThrowsCreditLimitExceeded:45` (spec's exact 5,000 / 4,600 / 650 numbers, asserts code + all three values) + integration 409 above + `Submit_DraftOrderWithinCreditLimit_AdvancesToSubmitted:217` | ⚠️ PARTIAL — behavior fully covered; produced literal differs (no thousands separators, extra `(customer '…')` suffix — `CreditLimitExceededException.cs:42-44`) and no test asserts the literal string → W7 |
| Scenario SL-03 (POS multi-tender checkout) | POS | Deferred — `spec.md:80` | ⏭️ OUT OF SCOPE (DEFERRED) |
| Scenario SL-04 (idempotent `SalesInvoice` submission) | replay guard | Deferred — `spec.md:94` (POS route unguarded, no replay test) | ⏭️ OUT OF SCOPE (DEFERRED) |
| Scenario SL-05 (cancellation & credit note) | cancel | Deferred — `spec.md:103` (no handler/endpoint/test) | ⏭️ OUT OF SCOPE (DEFERRED) |
| Scenario SL-06 (concurrency on credit + stock fulfilments) | credit/stock races | Deferred — `spec.md:112`, which also carries W1 as an explicit "related carry-forward safety flag" | ⏭️ OUT OF SCOPE (DEFERRED) — W1 remains an active module WARNING (below) |

**Compliance summary**: 10 spec rows total → **4 in scope: 3 COMPLIANT, 1 PARTIAL, 0 UNTESTED, 0 FAILING**; **6/10 rows OUT OF SCOPE (DEFERRED by amendment `1a17c866`)** — excluded from scoring per the amendment, not counted as UNTESTED or CRITICAL.

## Correctness (Static Evidence — tasks)

| Task | Status | Notes |
|------|--------|-------|
| 5.1 Customer domain & credit controls | ✅ Implemented & in scope | `Erp.Domain/Entities/Customer.cs` (CreditLimit, BypassCreditLimitCheck, RowVersion:83) + `Erp.Domain/Services/CreditControlEvaluator.cs:21` + `Features/Selling/Commands/CreateCustomerCommandHandler.cs`; 8 + 7 + 4 passing tests (`CreditControlEvaluatorTests`, `CreateCustomerCommandHandlerTests`, `CustomersApiTests`) — re-run green this round |
| 5.2 Sales order & commitment fulfillment | ✅ Implemented & in scope | `SalesOrder`/`SalesOrderItem` + `CreateSalesOrderCommandHandler` (gapless `SO-2026-xxxxx`, server-side totals) + `SubmitSalesOrderCommandHandler.cs:64` credit gate; 13 unit methods (18 cases) + 5 API tests (gapless, GET roundtrip, submit 200, credit 409, transition 409) |
| 5.2b Delivery Note posting & fulfillment (A1) | ✅ Implemented & in scope (W1 caveat) | `SalesPostingService.cs` (phase-1 SL-04 guards :114-158, FIFO :160-181, `DoubleEntryGuard` :184, order advance :226-227), `PostDeliveryNoteCommandHandler`, `DeliveryNotesController.cs:117` `[IdempotencyKeyRequired]`, gapless `DN-2026-xxxxx` (`SalesRepository.cs:110` `MAX … WITH (UPDLOCK, HOLDLOCK)`); 16 unit + 6 API tests green — FIFO range-lock gap → W1 |
| 5.3 Sales Invoice posting & General Ledger | ⚠️ Checked `[x]`, now DEFERRED by spec (`spec.md:31,44`) | Code unchanged and still unrouted/`TaxTotal = 0`/zero tests; `BilledPercentage`/`BilledQuantity` still never written → W5; `tasks.md` still shows `[x]` with no deferral note → W11 |
| 5.4 Atomic POS Register Checkout | ⚠️ Checked `[x]`, now DEFERRED by spec (`spec.md:47,80`) | Code unchanged: no COGS/stock pair, no FIFO/negative-stock guard, selling-rate valuation, unguarded endpoint, zero tests; `tasks.md` still `[x]` → W11 |
| 5.5 React Sales Studio & POS Cashier UI | ⚠️ Checked `[x]`, POS half DEFERRED (`spec.md:80`); in-scope UI gaps → W11/SUGGESTION | `SellingOverview.tsx` (mock invoices :7-11, dead "New Sales Invoice" button :23-29) + `PosCashierModal.tsx` (tender buttons :195-212); no order/delivery list UI, no test runner |

## Design Coherence (plan.md)

| Decision | Followed? | Notes |
|----------|-----------|-------|
| §1 Customer DDL (temporal, `UQ_Customer_Tenant_Company_Code`, `CK_CreditLimit >= 0`) | ✅ Yes | `AddModule0405Updates.cs:897`, `CustomerConfiguration.cs:35` (RowVersion) / `:73-74` (unique index), temporal config retained |
| §1 SalesOrder / SalesOrderItem DDL + `RowVersion` | ✅ Yes | `SalesOrderConfiguration.cs:38` `IsRowVersion`; plan columns match (`DeliveredQuantity`, `BilledQuantity`, totals CHECKs) — but `BilledQuantity/BilledPercentage` never written → W5 |
| §1 SalesInvoice / SalesInvoiceItem + `UQ_…InvoiceNo` | ⚠️ Partially (now mirrored by spec deferral) | Header table exists (`AddModule0405Updates.cs:626`, unique index `:984`, `SalesInvoiceConfiguration.cs:28`); **Amendment A2's `SalesInvoiceLine` with TaxRate/TaxAmount still absent** (`SalesInvoiceItem.cs` has no tax columns) → W6 (plan.md was NOT amended — `1a17c866` touched only spec.md) |
| §1 POSProfile DDL | ⚠️ Deviation | Table exists with an extra `IncomeAccountId` (FK `FK_POSProfile_IncomeAccount`) not in plan §1.5; feature itself deferred |
| §1 DeliveryNote / DeliveryNoteLine (Amendment A1) | ✅ Yes | `AddModule0405Updates.cs:733/803`, `UQ_DeliveryNote_Tenant_Company_VoucherNo` at `:918`, `DeliveryNoteConfiguration.cs:25/57-58` |
| §1 Company selling GL defaults (Amendment A2: `ReceivableAccountCode`, `SalesRevenueAccountCode`, `OutputTaxPayableAccountCode`) | ⚠️ Deviation | Shipped as `DefaultReceivableAccountCode`/`DefaultIncomeAccountCode` (`Company.cs:101-103`, migration `:489/482`); **`OutputTaxPayableAccountCode` does not exist** → W6 |
| §2 `CreditControlEvaluator` logic | ✅ Yes | `Erp.Domain/Services/CreditControlEvaluator.cs` matches plan semantics (bypass/zero-limit short-circuits, strict `>`), 8 tests green this round |
| Concurrency mechanism | ➖ Not in design | Design silent for selling; DN takes only the gapless-number `UPDLOCK/HOLDLOCK` (`SalesRepository.cs:110`) **after** the FIFO read → W1 |

## Issues Found

**CRITICAL**: None. The five original CRITICALs (C1–C5) are **retired to OUT OF SCOPE** by the approved scope amendment: their subject rows now carry accurate inline DEFERRED markers (`spec.md:31,47,80,94,103,112`) and are excluded from scoring; the underlying code conditions were re-verified this round as unchanged (docs-only commits since `2460d84e`) and each marker's factual claims match the source exactly (see "Amendment integrity check"). Build 0/0, orchestrator full suite 370/370 at this code state, my narrow re-runs 49/49 green; every in-scope requirement has at least one passing covering test.

**WARNING**:

- **W1 (carry-forward SAFETY FLAG — confirmed unchanged, still WARNING, not worse)** — `SalesPostingService.PostDeliveryNoteAsync` still reads FIFO layers without the stock range lock: `grep LockStockRangeAsync src/Backend` matches only `StockPostingService.cs:87`; the selling path calls `_stock.GetFifoLayersAsync` at `SalesPostingService.cs:403` → `FifoValuation.BuildLayers` at `:415-416` (reached from `:167`), while its only `UPDLOCK/HOLDLOCK` is the voucher-number `SELECT MAX … FROM DeliveryNote` at `:193` — i.e. **after** the layers were read, so it serializes writes but not the stale read. Two *different* sales orders shipping the same (item, warehouse) concurrently can both pass `FifoValuation.Consume`'s `AllowNegativeStock = false` guard and double-consume layers, risking `ST-03` (`.specify/specs/02-stock/spec.md:41-43`; module archived at `e599ecac`) and contradicting 02-stock Task 3.9's "row locks prevent overselling"; the class is proven fixed only for stock entries (`StockEntriesConcurrencyApiTests.cs:60,136`), never for delivery notes, and `FifoValuation.cs:92-97` documents the prior "pre-Task-3.9 concurrency bug". **Ranking decision: WARNING (carry-forward), not CRITICAL** — no in-scope selling scenario exercises or asserts this race (same-order races are covered by `SalesOrder.RowVersion` + `SalesPostingServiceTests.cs:438`), no test fails, and the risk is now explicitly disclosed at `spec.md:112` as an expected carry-forward flag. It is *not* dismissed: remediation is a one-line call (`IStockRepository.LockStockRangeAsync` is already on `_stock`) before phase 2, plus a two-orders-one-item concurrency test. It would escalate to CRITICAL the moment a selling scenario or test asserts non-overselling under concurrency.
- **W5 (dead plan/task columns)** — `SalesOrder.BilledPercentage` / `SalesOrderItem.BilledQuantity` are still assigned nowhere in selling code (grep `BilledPercentage =|BilledQuantity +=` → only migrations, `CreateSalesInvoiceCommandHandler`-side initial 0, and `PurchasePostingService.cs:605,620` for *purchase* orders). The green API test pins the gap: `SalesOrderDeliveryNoteApiTests.cs:295,300` assert `0m` with the comment "Task 5.3 owns it". Root backlog `.specify/tasks.md:128` still promises "Three-way matching tracks `DeliveredQuantity` and `BilledQuantity`". The billing half is now deferred by spec, but the plan/task documents were not updated → drift.
- **W6 (plan.md not amended)** — `1a17c866` changed `spec.md` only; `plan.md` still carries Amendment A2 (`SalesInvoiceLine` with TaxRate/TaxAmount, Company `ReceivableAccountCode`/`SalesRevenueAccountCode`/`OutputTaxPayableAccountCode`) which does not exist in the schema (`SalesInvoiceItem.cs`, `Company.cs:101-103`). The approved design document now disagrees with both the code *and* the amended spec.
- **W7 (in-scope literal drift on SL-02)** — Scenario SL-02 is **still in scope**; `CreditLimitExceededException.cs:42-44` renders `Credit limit $5000 exceeded. Current: $4600, Attempted: $650 (customer 'ACME Corp').` vs the spec's `Credit limit $5,000 exceeded. Current: $4,600, Attempted: $650`; grep `Credit limit \$` in `tests/` → 0 matches (no test asserts the literal). Behavior and numbers are tested; the exact message contract is not.
- **W10 (in-scope coverage gap — DN idempotent replay)** — the certified `DeliveryNotesController` POST carries the guard (`:117`) but only the missing-key 400 is tested (`SalesOrderDeliveryNoteApiTests.cs:494`); no test proves "replay → 200 + byte-identical body + zero new ledger rows" on this endpoint, unlike the purchase side (`PurchaseInvoiceApiTests.cs:81,303`). Shared filter proven elsewhere; this endpoint's replay behavior is unproven.
- **W11 (task/status document drift after the amendment)** — the amendment touched only `modules/03-selling/spec.md`; so three documents now disagree about 03-selling: `modules/03-selling/tasks.md` still shows Task 5.3/5.4/5.5 `[x]` with no deferral note while their spec rows are DEFERRED; root `.specify/tasks.md` Phase 5 (selling) is entirely `[ ]` (14 unchecked overall, incl. Tasks 5.1–5.4) while its own index row says `03 … 100% CERTIFIED`; and `.specify/spec.md:15` / `.specify/plan.md:14` / `.specify/tasks.md:14` still read `100% CERTIFIED` for 03 (orchestrator has acknowledged the index rows will be corrected in the archive commit — the module `tasks.md` annotation should ride along in that same commit).

**Former findings reclassified to DEFERRED / OUT OF SCOPE (no longer scored, kept as carry-forward flags in the archive report)**: W2 (POS bypasses FIFO/negative-stock guard and books no COGS/stock pair — `spec.md:47`), W3 (POS endpoint lacks `[IdempotencyKeyRequired]`, Constitution VI.4 — `spec.md:94`), W4 (no reachable code updates `Customer.OutstandingAmount` — `spec.md:44`), W8 (invoice path skips Constitution III.3 pre-write account validation, `AccountId` can reach `Guid.Empty` — `spec.md:31`), W9 (Task 5.5 acceptance depends on the deferred POS path — `spec.md:80`; residual UI polish moved to SUGGESTION).

**SUGGESTION**:

- Track the deferred set as an explicit backlog item in the archive report: route `CreateSalesInvoice`/`SubmitSalesInvoice` **or delete them**, implement SL-05 cancellation (mirror `CancelPurchaseInvoiceCommandHandler`), guard + test the POS endpoint, then re-certify.
- Ship the W1 remediation together with a `DeliveryNote`-vs-`DeliveryNote` concurrency test beside `StockEntriesConcurrencyApiTests` — cheapest possible insurance for `ST-03`.
- Assert the exact SL-02 message literal in `CreditControlEvaluatorTests`, or amend the spec to the produced format (either direction closes W7).
- Add a delivery-note replay test (200 + identical body + no new ledger rows) to close W10.
- Align `plan.md` Amendment A2 and `modules/03-selling/tasks.md` with the amended spec in the archive commit, alongside the acknowledged root index fix.
- Add a frontend test runner to `erp-client`; replace the mock invoice table and dead "New Sales Invoice" button when the sales-invoice scope is un-deferred.

## Verdict

**PASS WITH WARNINGS**

At HEAD `1a17c866` the build is clean (0 warnings/0 errors, third attempt — attempts 1–2 hit file-lock errors from a concurrent build, not code), the orchestrator's full suite is 370/370 at a source tree I independently confirmed is unchanged since `2460d84e` (all later commits are `.specify/`-only per `git log --stat`), and my module-scoped re-runs are green (41/41 Application, 8/8 Domain). Against the **amended** spec, all four in-scope rows have passing covering tests — Invariant SL-04 and the certified DeliveryNote half of SL-01 are fully compliant, the SalesOrder path of Invariant SL-02 is fully compliant — leaving one PARTIAL (SL-02's message literal, W7). The six deferred rows are excluded by an amendment whose factual claims I re-verified line-by-line against the source, so the previous CRITICALs C1–C5 legitimately retire rather than being hidden: they are recorded as DEFERRED markers and remain visible carry-forward flags. What keeps this from being a clean PASS is genuine, still-open work: W1 (the missing `LockStockRangeAsync` in `SalesPostingService.PostDeliveryNoteAsync` — re-confirmed at `:403`, now spec-disclosed, ranked WARNING because no in-scope scenario exercises the race), W5/W6/W11 (plan, module tasks and root index now disagree with the amended spec), W7 (in-scope literal drift) and W10 (unproven DN replay). **Compliance summary: 4/10 rows in scope → 3 COMPLIANT, 1 PARTIAL, 0 UNTESTED, 0 FAILING; 6/10 rows OUT OF SCOPE (DEFERRED, excluded); 0 CRITICAL, 6 WARNING (W1, W5, W6, W7, W10, W11), 5 findings reclassified DEFERRED, 6 SUGGESTION.**
