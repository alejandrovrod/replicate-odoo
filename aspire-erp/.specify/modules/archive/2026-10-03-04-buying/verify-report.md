# Verification Report — 04-buying

**Change**: 04-buying
**Version**: 2.0.0 (spec.md)
**Mode**: Standard (Strict TDD INACTIVE — no `strict_tdd` config, no TDD runner → `strict-tdd-verify.md` not loaded)

## Completeness

| Metric | Value |
|--------|-------|
| Tasks total | 7 |
| Tasks complete (checkbox `[x]`) | 7 |
| Tasks incomplete | 0 |
| Tasks verified against real evidence (code + passing tests) | 6 fully verified, 1 verified with acceptance gaps (Task 4.5) |
| Artifacts present | specs ✅, design ✅, tasks ✅, proposal ❌ (missing by design), applyProgress = tasks.md checkboxes |
| Skipped dimensions | proposal (artifact absent), coverage measurement (no collector configured), Strict-TDD checks (inactive), frontend automated tests (no test runner in `erp-client`) |

## Build & Tests Execution

**Build**: ✅ Passed

```text
> dotnet build Erp.sln --nologo -v q   (workdir C:\Workspace\Odoo\aspire-erp)
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:01:37.91
```

**Tests**: ✅ 347 passed / ❌ 0 failed / ⚠️ 0 skipped

```text
> dotnet test Erp.sln --no-build --nologo
Passed!  - Failed: 0, Passed:  86, Skipped: 0, Total:  86, Duration: 188 ms - Erp.Domain.UnitTests.dll (net10.0)
Passed!  - Failed: 0, Passed: 216, Skipped: 0, Total: 216, Duration: 704 ms - Erp.Application.UnitTests.dll (net10.0)
Passed!  - Failed: 0, Passed:  45, Skipped: 0, Total:  45, Duration: 39 s - Erp.Api.IntegrationTests.dll (net10.0)
(exit code 0; totals match the recorded 86 + 216 + 45 = 347)
```

**Frontend build (Task 4.5 evidence)**:

```text
> npm run build   (workdir src\Frontend\erp-client → "tsc -b && vite build")
✓ 1969 modules transformed. dist/assets/index-Col9nK2b.js 368.41 kB
✓ built in 3.44s   (0 TypeScript errors)
```

**Coverage**: ➖ Not available / threshold: not configured → no coverage collector in the solution; recorded as a skipped check.

**Repository state**: HEAD `bdca3466`, `git status --porcelain` empty — verify made no edits, no commits, no `.specify/` writes.

## Spec Compliance Matrix

| Requirement | Scenario | Test | Result |
|-------------|----------|------|--------|
| Invariant BY-01 (Dr Stock In Hand / Cr SRNB = Qty × ValuationRate) | accrual equation | `PurchasePostingServiceTests > PostReceiptAsync_SingleLine_WritesBalancedAccrualAndKardexRows` (asserts Dr 1310 1000 / Cr 2120 1000, SLE +10 @ 100) | ✅ COMPLIANT |
| Invariant BY-02 (clearance + tax + payable, ΣDr−ΣCr = 0.0000) | equation | `PostInvoiceAsync_By01_ClearsAccrualBooksTaxAndPayable` + `PurchasePostings_SatisfyDoubleEntryZeroSumInvariant` | ✅ COMPLIANT (withholding term always 0 — see Issues W4) |
| Invariant BY-03 (Billed ≤ Accepted − previously billed) | overbilling ceiling | `ThreeWayMatchValidatorTests` (6 tests) + `PostInvoiceAsync_QuantityAboveReceived_FailsWithOverbillingNotAllowed` | ✅ COMPLIANT |
| Scenario BY-01 | Goods receipt with interim accrual (Dr 1310 / Cr 2120, stock +N in StockLedgerEntry, PO-linked) | `PostReceiptAsync_SingleLine_WritesBalancedAccrualAndKardexRows` + `PostReceiptAsync_OrderedOrder_AdvancesWorkflowToReceived`; live receipt POSTs asserted 201 in `PurchaseInvoiceApiTests` / `PurchaseInvoiceOverbillingApiTests` | ✅ COMPLIANT |
| Scenario BY-02 | Invoice clears accrual, debits input tax, credits AP, accrual balance $0.00 | `PostInvoiceAsync_By01_ClearsAccrualBooksTaxAndPayable` (Dr 2120 1000 / Dr 1130 100 / Cr 2110 1100) + live 3-row posting asserted in `Create_ReplayedWithSameKey_…` | ⚠️ PARTIAL — GL shape and amounts match, but spec literal is `1350 - Input VAT Recoverable` while chart/implementation use `1130 - Input Tax Recoverable`; the "$0.00 accrual balance" clause is implied (equal-amount clearing), never asserted directly |
| Scenario BY-03 | Reject overbilling, exact message, **no ledger entries** | `PurchaseInvoiceOverbillingApiTests > Create_BillAboveReceivedQuantity_Returns400By03AndWritesNoLedgerRow` (GL totals snapshot unchanged) + `ThreeWayMatchValidatorTests > ValidateBillingQuantity_FirstBillAboveReceived_ThrowsBy03Message` + unit posting test | ✅ COMPLIANT |
| Scenario BY-04 | Idempotent replay → HTTP 200 + identical body, duplicates prevented | `PurchaseInvoiceApiTests > Create_ReplayedWithSameKey_Returns200WithIdenticalBodyAndBooksNothingNew` (byte-identical body, ledger row set unchanged, missing-key 400) + `IdempotencyPolicyTests` (7 tests) | ✅ COMPLIANT |
| Scenario BY-05 | Cancel → `Cancelled`, reversing GL (sides swapped), AP restored, **stock intake reversed if associated PR returned** | `PurchaseInvoiceApiTests > Cancel_PostedInvoice_Returns200AppendsSwappedReversalAndRestoresPayable` + `Cancel_Twice_…` + `CancelPurchaseInvoiceCommandHandlerTests` (11 tests) | ⚠️ PARTIAL — first two clauses fully covered; the stock-reversal clause has NO implementation and NO test (handler only appends the GL mirror; `IStockRepository` is used solely to read/append `GLEntry`) |
| Scenario BY-06 | Row lock serializes two clerks; loser gets `Only 5 units remaining to bill, 15 requested` | `PurchaseInvoiceOverbillingApiTests > Create_ConcurrentBillsAgainstOneReceipt_LetsExactlyOneThroughAndBlocksTheRest` (exactly one 201 / one 400 with the BY-06 literal, progressive billing afterwards) + `ValidateBillingQuantity_SecondConcurrentBillAboveRemaining_ThrowsBy06Message` | ✅ COMPLIANT — lock evidence: `PurchaseRepository.cs:320` `SELECT Id FROM dbo.PurchaseReceiptLine WITH (UPDLOCK, HOLDLOCK)`, previously-billed read as SUM via `GetBilledQuantityForReceiptLineAsync` |

**Compliance summary**: 7/10 rows COMPLIANT, 3/10 PARTIAL, 0 FAILING, 0 UNTESTED scenarios.

## Correctness (Static Evidence — tasks)

| Task | Status | Notes |
|------|--------|-------|
| 4.1 Supplier master | ✅ Implemented | `Supplier` entity (Code, TaxId, DefaultPayableAccountId, BillingCurrency, PaymentTermsDays) + `PurchaseValidator.EnsureValidSupplierFields`; 5 passing tests (duplicate/missing code/name) |
| 4.2 PO workflow | ✅ Implemented | Draft→`Submitted`, immutability after submit (`Update_AfterSubmit_Returns409InvalidStatusTransition`, unit + integration), gapless `PO-2026-00001/2` (`Create_TwoConsecutiveOrders_ReturnsSequentialGaplessOrderNumbers`); progress fields caveat → W5/W6 |
| 4.3 Receipt & accrual | ✅ Implemented | `PurchasePostingService.PostReceiptAsync`: SLE + balanced GL + gapless `PR-2026-00001`, one transaction |
| 4.4 3-way match | ✅ Implemented | `ThreeWayMatchValidator` + `UPDLOCK` line lock; exact spec literals; rejected bill writes nothing |
| 4.5 React Procurement Studio | ⚠️ Implemented with acceptance gaps | `BuyingOverview.tsx` exists, builds clean (`tsc -b`), PO status list + `PurchaseReceiptModal`; but the "Unbilled Receipts" card always renders 0 (see W5), no vendor-aging cards, no frontend tests |
| 4.6 Idempotency & reversal | ✅ Implemented | `IdempotencyFilter` (resource filter, SHA-256 raw body, 200 replay / 409 in-progress / 409 key reuse) + `CancelPurchaseInvoiceCommandHandler` (mirror rows, status gate, freeze gate) |
| 4.7 Concurrency integration tests | ✅ Implemented | Live concurrent-bill test against `erp-sqlserver` passed in verify run (45/45 integration) |

## Design Coherence (plan.md)

| Decision | Followed? | Notes |
|----------|-----------|-------|
| §1 Schema: `Supplier`, `PurchaseOrder`, `PurchaseOrderItem`, `PurchaseReceipt`, `PurchaseInvoice` tables | ⚠️ Partially | Tables exist via EF migration `AddModule0405Updates` and are exercised by passing integration tests. Deviations: `SupplierCode/SupplierName` → `Code/Name`; `Supplier.CompanyId` FK absent (tenant-scoped); plan-required `SYSTEM_VERSIONING` temporal history NOT applied to `Supplier` (only Account/Company/Customer are temporal); `ReceiptNumber`/`InvoiceNumber` → `VoucherNo`; plan DDL omits `PurchaseReceiptLine`/`PurchaseInvoiceLine` entirely (required by §2) |
| §1 `RowVersion` concurrency columns | ✅ Yes | Present on PO/PR/PI entities as EF concurrency tokens |
| §2 `ThreeWayMatchValidator` logic | ⚠️ Deviation (spec wins) | Gate logic identical; sample message text is stale: plan says `Maximum remaining billable quantity is X`, spec/impl say `Cannot bill N units. Maximum receivable: X`, and plan §2 has no BY-06 `Only X units remaining…` branch (flag a corroborated) |
| Concurrency mechanism | ➖ Not in design | Design silent; implementation uses `UPDLOCK, HOLDLOCK` on receipt lines — satisfies spec BY-06 (verified live) |

## Issues Found

**CRITICAL**: None. Build 0/0, tests 347/347 exit 0, every spec scenario has at least one covering test that passed in the verify run.

**WARNING**:

- **W1 (spec gap, flag confirmed)** — Spec BY-05's third clause ("physical stock intake is reversed if associated with a returned `PurchaseReceipt`") is neither implemented nor tested: `CancelPurchaseInvoiceCommandHandler` only appends GL mirror rows. Scenario BY-05 is PARTIAL.
- **W2 (spec/chart mismatch)** — Spec BY-02 names `1350 - Input VAT Recoverable`; account `1350` does not exist anywhere in src or `scripts/seed-dev-buying.sql` (which seeds `1130 - Input Tax Recoverable`, used by the passing test). Reconcile spec or chart.
- **W3 (flag a — corroborated)** — `plan.md` §2 validator sample text is stale vs the spec literals for BY-03/BY-06; implementation follows the spec, so the approved design document is out of sync.
- **W4 (scope ambiguity)** — Tax withholding (spec §1 glossary + the `Tax Withholding Payable` term of invariant BY-02) has no implementation path: `WithholdingTaxTotal` is hard-coded `0` in `PurchasePostingService.cs:359`, no withholding account/endpoint/scenario exists. The invariant still holds and is tested at 0; no Gherkin scenario covers withholding, so this is not scored `UNTESTED`, but spec §1 promises a concept the module does not deliver.
- **W5 (Task 4.5 acceptance unmet)** — `PurchaseOrder.ReceivedPercentage`, `BilledPercentage` and `PurchaseOrderItem.ReceivedQuantity/BilledQuantity` are never assigned anywhere in `src\Backend` (grep: only configs/migrations/DTO mappings). The UI's `unbilledAmount` filters `receivedPercentage > 0 && billedPercentage < 100` → **the "Unbilled Receipts (Accrued)" card always shows $0.00**; the component's own comment calls them "mock metrics".
- **W6 (workflow shortcut)** — `PurchasePostingService.cs:391-393`: any invoice against a receipt of an order sets `order.Status = Completed` ("In a real implementation we would check if ALL quantities are fully billed"), so a partial bill closes the PO; combined with W5 the progress columns stay 0. Outside spec scenarios, but weakens Task 4.2 "status tracking".
- **W7 (flag d — corroborated)** — XML docs say `Draft -> Ordered -> Received -> Billed` while the enum/submit path is `Submitted`: `PurchaseOrder.cs` (lines 6, 43, 62), `CreatePurchaseOrderCommand.cs:11`, `SubmitPurchaseOrderCommand.cs:7`, `SubmitPurchaseOrderCommandHandler.cs:10`, `PurchaseOrdersController.cs:13,157`, `PurchaseOrderConfiguration.cs:24`, plus test name `Submit_DraftOrder_AdvancesToOrdered` which asserts `PurchaseOrderStatus.Submitted`.
- **W8 (flag b — corroborated)** — `PurchaseInvoiceDto` exposes no `RowVersion`, yet `CancelPurchaseInvoiceCommand` accepts an optional `RowVersion` token (`PurchaseInvoicesController.cs:156` "only an optimistic concurrency token") → clients cannot obtain the token they are invited to send; optimistic concurrency on invoice cancel is unusable via the API.
- **W9 (flag e — corroborated)** — `PurchaseErrorCodes.InvoiceAlreadyExists` is mapped in `PurchaseInvoicesController.cs:111` but never thrown anywhere in `src` (only 2 references), and `BillNumber` has no unique index → dead error path; duplicate vendor bills under different idempotency keys are accepted silently.
- **W10 (flag f — partially corroborated)** — `IdempotencyPolicy` enum doc still says "replay the stored **status** + body verbatim" while `IdempotencyFilter.cs:179-184` deliberately forces 200 instead of echoing the stored status (its own remark is accurate). `PurchaseReceiptsController.cs:64` ("replayed key returns the stored response verbatim") is body-accurate but never exercised: there is no receipt-replay integration test (the shared filter is proven only via invoices).

**SUGGESTION**:

- Task 4.1's currency branch (`EnsureValidSupplierFields` currency argument) has no test — only code/name/duplicate paths are covered.
- Add an assertion for the "$0.00 interim accrual balance" clause of BY-02 against the trial balance / general-ledger report.
- Decide whether `BillNumber` should be unique per tenant/company (ERPNext rejects duplicate supplier invoices).
- `erp-client` has no test runner; UI tasks are currently verified by type-checked build only.
- Align `plan.md` §1 DDL with reality (line tables, column names, temporal versioning decision) so the design stops drifting from the implementation.

## Verdict

**PASS WITH WARNINGS**

Build 0/0, 347/347 tests green in the verify run, all 6 spec scenarios and 3 invariants have passing covering tests; the three PARTIAL rows (BY-02 account literal, BY-05 stock-reversal clause, withholding term) and the Task 4.5 dead metric are spec/doc drift and scope gaps that should be reconciled before archive, but none is a failing or untested scenario.
