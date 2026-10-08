# Implementation Tasks: Multi-Currency FX — Exchange Rates, Realized FX, Revaluation (R-14)

**Module:** `14-fx-revaluation`
**Specification:** [spec.md](./spec.md) · **Technical Plan:** [plan.md](./plan.md)
**Status:** DRAFT — pending review (Fase 1; no code touched)

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

**Dependency:** R-12 archived (roadmap ordering rule; hooks extend the R-12 handlers in place)

> Each task is atomic and testable. Checkbox `[ ]` = pending Fase 2/3. Invariant tags (FX-xx) map to spec §2; scenarios map to spec §3.

---

## Phase 1: Domain — rates, voucher, math, error vocabulary

- [x] **Task 1.1: `ExchangeRate` entity + `ExchangeRateValidator` (FX-01)**
  - Action: create `Erp.Domain/Entities/ExchangeRate.cs` (GLOBAL, no `TenantId` — `Currency` precedent; `From/ToCurrencyId, RateDate, Rate, RowVersion, CreatedAt`) with guards (rate > 0, no self-pair); pure `ExchangeRateValidator` (field rules + allowance range `[0,1)` shared with Task 1.2).
  - Acceptance (unit): negative/zero rate throws; self-pair throws; allowance −0.1/1/1.5 throws, 0.0/0.5 passes (ERPNext range test mirrored).

- [x] **Task 1.2: `ExchangeRateRevaluation` + `ExchangeRateRevaluationLine` entities (FX-05/FX-06/FX-07)**
  - Action: create voucher entity (`TenantId, CompanyId, VoucherNo, PostingDate, ExchangeGainLossAccountId?, RoundingLossAllowance, DocumentStatus, Remarks, IdempotencyKey, RowVersion`) with `EnsureCanSubmit()/EnsureCanCancel()` (`Draft→Submitted→Cancelled` only) + future-date guard; line snapshot entity (FC/CC balances, current/new rates, gain/loss ≠ 0 CHECK-shape, `ZeroBalance`).
  - Acceptance (unit): transition matrix — `Submitted→Submit` throws, `Draft→Cancel` throws, `Cancelled→*` throws; future date throws; allowance outside `[0,1)` throws.

- [x] **Task 1.3: Document-currency extension on invoices + payment + company (FX-03/FX-04)**
  - Action: add nullable `CurrencyId` + `ExchangeRate = 1` (> 0 guard when foreign set) to `SalesInvoice`/`PurchaseInvoice`; nullable `TransactionCurrencyId` + write-once `SettlementExchangeRate` to `PaymentEntry`; `DefaultExchangeGainLossAccountId + Code` (code-not-FK, R-13 pattern) to `Company`.
  - Acceptance (unit): null-currency documents construct exactly as today (legacy default path); foreign currency with rate ≤ 0 throws; settlement rate re-assignment after submit throws.

- [x] **Task 1.4: `FxErrorCodes` + exception taxonomy (spec §5)**
  - Action: create Accounting-owned `FxErrorCodes` (all snake_case codes from spec §5); typed exceptions per plan §1; NO new `BankingErrorCodes` constants (single owner rule).
  - Acceptance (unit): every code resolves to its §5 HTTP status via the API mapping table; no duplicate string literals in handlers.

- [x] **Task 1.5: `FxCalculator` pure math service (FX-02/FX-05)**
  - Action: pure `RealizedPerSlice(allocFC, settlementRate, invoiceRate)`, `UnrealizedPerLine(balanceFC, balanceCC, newRate, allowance) → line?` (null = drop row), `AssertBalanced(lines)` (±0.0001, else `DoubleEntryImbalance`).
  - Acceptance (unit): FX-R1 fixture (1000 × (1.15−1.10) = +50 gain); FX-R2 mirror (loss); zero-diff → skipped; FX-R6 (10,000 × 0.05 = +500); FX-R7 payable loss; FX-R14 dust-within-allowance clears, dust-above-allowance throws; empty set → `NoRevaluationGainLoss`.

## Phase 2: Infrastructure — DDL, repositories, seed

- [x] **Task 2.1: Migration `AddFxRevaluation` (plan §2)**
  - Action: `ExchangeRates` (global, `CK_EXR_Rate`, `CK_EXR_NoSelfPair`, `UQ_EXR_Pair_Day`, `IX_EXR_Lookup`), `ExchangeRateRevaluations` (`CK_ERV_Status`, `CK_ERV_Allowance`, idempotency + voucher-no uniques, status/date index), `ExchangeRateRevaluationLines` (`CK_ERVL_NonZero`, voucher index), touch-ups (invoice `CurrencyId/ExchangeRate` + CHECKs, payment `TransactionCurrencyId/SettlementExchangeRate`, company FX default FK + code).
  - Acceptance (integration): scratch-DB migrate succeeds; duplicate pair-day insert fails; self-pair fails; rate ≤ 0 fails; allowance ≥ 1 fails at the DB; names confirmed against `AppDbContextModelSnapshot` first (plan §2 note).

- [x] **Task 2.2: `ExchangeRateRepository` + §3.1 resolution (FX-01)**
  - Action: `IExchangeRateRepository` (`ResolveRateAsync` direct→inverse-reciprocal→throw, `UpsertAsync`, `GetAsync`, date list); `ExchangeRate` registered WITHOUT tenant query filter (global-catalog precedent).
  - Acceptance (integration): direct hit; inverse-only row returns `1/rate` @6dp; absent both → `exchange_rate_missing`; `from == to` returns 1 with zero DB reads; concurrent upsert of the same pair-day serializes on the unique index.

- [x] **Task 2.3: `ExchangeRateRevaluationRepository` — §3.4 balance query (FX-05)**
  - Action: FY-agnostic cutoff query (`PostingDate ≤ date`, non-cancelled, Asset/Liability leaves, FC ≠ functional, warehouse-stock exclusion via W4 linkage, dust/allowance keep-rule) shared verbatim by preview + submit; `AddLinesAsync`, `AddReversalEntriesAsync`, serializable submit lock helper. Implements the plan §3.4 fail-closed fallback when the W4 linkage is unresolvable.
  - Acceptance (integration): seeded FC receivable/payable/bank inside cutoff returned with exact FC+CC nets; P&L/Equity leaves never returned; same-currency leaves never returned; warehouse stock account excluded; post-submit re-query is a fixpoint (empty).

- [x] **Task 2.4: Seed `Exchange Gain/Loss` P&L leaf + wire company default (FX-03 enabler)**
  - Action: seed script/migration ensuring one active P&L leaf `Exchange Gain/Loss` per company (recommend Expense root, code e.g. `4290`) and backfilling `Company.DefaultExchangeGainLossAccountId/Code` where null (R-13 task 2.5 precedent — without this, every FX submit fails FX-03).
  - Acceptance (integration): fresh company has exactly one such leaf and the default chain resolves; submit with null voucher account posts to it (FX-R1).

## Phase 3: Application — CQRS handlers (+ R-12 surgical extensions)

- [x] **Task 3.1: `UpsertExchangeRate` + `GetExchangeRate` (FX-01)**
  - Action: commands with validators + `RowVersion` on correction; idempotency is the natural key (same pair + day + rate replays return the row, no header); lookup returns `EffectiveRate + IsInverse`.
  - Acceptance (integration): create → correct (stale token → `concurrency_conflict`) → lookup each scenario in FX-R15; identical-payload replay performs zero writes.

- [x] **Task 3.2: `CreateExchangeRateRevaluation` — Draft with pre-checks (FX-03/FX-07)**
  - Action: binds company + date, validates allowance range, frozen + open-year + future-date guards, FX-account pre-resolution (explicit or company default), idempotent replay.
  - Acceptance (integration): closed-year date → `fiscal_year_closed`; frozen date → `fiscal_period_locked`; future date → `revaluation_future_date`; bad FX account (group/inactive/wrong-type/wrong-company) → `invalid_exchange_gain_loss_account`; replay returns same Draft.

- [x] **Task 3.3: `SubmitExchangeRateRevaluation` (FX-05/FX-06 — the unrealized core)**
  - Action: implement plan §4 validation order in a SERIALIZABLE txn: guards → RowVersion → FX-account re-validation → §3.4 read → non-empty → `FxCalculator` per line (dust/drop rules) → balance assert → persist lines + GL + `Submitted` + `ERV-YYYY-NNNNN`.
  - Acceptance (integration): FX-R6 gain / FX-R7 loss / FX-R14 dust post exactly the §3 GL sets with `ΣD−ΣC=0`; FX-R10 replay yields ONE GL set; FX-R11 parallel submits yield ONE winner (loser → `no_revaluation_gain_loss`); preview output deep-equals persisted lines.

- [x] **Task 3.4: `CancelExchangeRateRevaluation` as reversal (FX-06)**
  - Action: Submitted-only guard; FY still open + frozen check on ORIGINAL date; mirrored reversal append (originals untouched); refuse on closed FY.
  - Acceptance (integration): FX-R13 — originals byte-identical, reversals net voucher to zero, second cancel → `revaluation_already_cancelled`, closed-FY cancel → `fiscal_year_closed`.

- [x] **Task 3.5: Payment submit/cancel FX branch — surgical extension of R-12 (FX-02/FX-04)**
  - Action: in `SubmitPaymentEntryCommandHandler` after PE-02/PE-07: mismatch check → resolve + store `SettlementExchangeRate` → per-slice `FxCalculator` → FX leaf resolution → CC-restated base lines + plug lines → balance assert (plan §3.2, same txn); in `CancelPaymentEntryCommandHandler`: mirror from STORED rate (plan §3.3); create-command payload + `TransactionCurrencyId`.
  - Acceptance (integration): FX-R1/R2 exact GL sets; FX-R3 no FX line; FX-R4/FX-R5/FX-R9 persist nothing; FX-R12 reversal (rows with `IsCancelled = true`, R-12 precedent — unlike revaluation reversals) nets to zero + FC outstanding restored + immune to later rate edits; FULL R-12 + invoice regression suite green with byte-identical single-currency postings.

- [x] **Task 3.6: `GetRevaluationPreview` + detail queries (read-only)**
  - Action: preview returns the exact line set submit would post (+ `TotalGainLoss`); detail returns voucher + lines + GL rows.
  - Acceptance (integration): preview deep-equals the lines persisted by the subsequent submit for the same company/date snapshot; preview of an unpostable (frozen/closed) date fails with the same code submit would raise.

- [x] **Task 3.7: Invoice submit CC restatement — sales + purchase (FX-04, without this FX-02 is unexecutable)**
  - Action: in the Sales Invoice submit (R-11) and Purchase Invoice submit paths, inside the existing posting transaction and BEFORE the first GL row: resolve `ExchangeRate` = catalog FC → CC at invoice `PostingDate` (skip when `CurrencyId` null → rate 1, byte-identical legacy path), store it on the invoice, post `Debit/Credit` CC-restated with FC columns in document currency. Missing rate → `exchange_rate_missing`, nothing persisted.
  - Acceptance (integration): FX-R17 rejection persists nothing; foreign invoice posts CC = FC × rate with FC columns intact; single-currency invoice postings byte-identical to pre-R-14 (full selling/buying regression green); a later payment against it realizes exactly per FX-R1 math.

## Phase 4: API — controllers + RFC 7807

- [x] **Task 4.1: `ExchangeRatesController` + `ExchangeRateRevaluationsController` (§5 routes)**
  - Action: all plan §5 routes; `Idempotency-Key` required on mutating routes; `rowVersion` (base64) required on submit/cancel/rate-correct; tenant/company scoping; payment routes untouched (payload-only change).
  - Acceptance (API integration): route × scenario matrix — each spec §5 code asserts its HTTP status + `code` in the ProblemDetails body; missing key → 400; stale rowVersion → 409.

## Phase 5: UI — rate catalog + revaluation execution screen

- [x] **Task 5.1: Rate catalog (`useExchangeRates.ts`, `ExchangeRateList.tsx`, `ExchangeRateFormModal.tsx`)**
  - Action: list (pair + date filter, paging) + upsert form (from/to/date/rate + guards surfacing §5 codes) + inverse-rate badge display.
  - Acceptance: `tsc + vite build + eslint` clean; validation errors localized (accounting namespace en+es).

- [x] **Task 5.2: Revaluation execution screen (list + modal flow)**
  - Action: company/date/allowance + FX-account picker + read-only preview grid (from `preview`) + total banner + Submit/Cancel gated on status, sending `Idempotency-Key` + `RowVersion`; every §5 code surfaced with localized strings.
  - Acceptance: `tsc + vite build + eslint` clean; preview → submit → status flip → cancel reversal visible in ledger; double-click submit posts once (key reuse).

## Phase 6: Verification — full invariant sweep

- [x] **Task 6.1: Adversarial suite + archive**
  - Action: run every Phase 1–5 acceptance; prove fixpoint (submit → empty preview), append-only ledger (no `UPDATE/DELETE` on `GLEntry` in either FX path), cross-pipeline lock coverage (revaluation date guards consistent with R-13 retrofits), single-currency regression zero-diff; record carry-forwards and archive per roadmap working agreement.
  - Acceptance: 100% scenarios FX-R1…FX-R17 green; zero contradictions between spec/plan/tasks; certification badge issued.
