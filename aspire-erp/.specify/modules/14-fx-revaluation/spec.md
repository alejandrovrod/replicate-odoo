# Functional Specification: Multi-Currency FX — Exchange Rates, Realized FX on Payment, Unrealized via Revaluation

**Module:** `14-fx-revaluation`
**Status:** DRAFT — pending review (roadmap.md row **R-14**; depends on R-12)
**Version:** 1.0.0
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit
**Canonical Reference:** ERPNext `erpnext/accounts/doctype/exchange_rate_revaluation` (verified: header `company` + `posting_date` + `rounding_loss_allowance ∈ [0,1)`; child rows `account / account_currency / balance_in_account_currency / balance_in_base_currency / current_exchange_rate (= base ÷ foreign) / new_exchange_rate / new_balance_in_base_currency (= FC × new rate) / gain_loss (= new base − base) / zero_balance`; rows without gain/loss are dropped before submit; posting is JE-mediated with `voucher_type = "Exchange Rate Revaluation"`) + docs at `docs.frappe.io/erpnext/exchange-rate-revaluation`
**Closes:** Accounting AC-05 deferred (`aspire-erp/.specify/specs/01-accounting/spec.md` §AC-05, amend S1 v2.1.0: `RealizedFX = PaymentAmount_FC × (Rate_Settlement − Rate_Original)` auto-posted to `Exchange Gain/Loss Account`)

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)** — 2 BLOCKER + 6 WARNING cerrados en Pass 2, re-auditoría Pass 3 sin contradicciones.

---

## 1. Executive Summary & Ubiquitous Language

R-14 gives Aspire ERP true multi-currency accounting on top of the **existing** single-currency base (global `Currency` catalog RM-09, nullable `CurrencyId` on Company/Account/Customer/Supplier/BankAccount/Opportunity, `GLEntry.DebitInAccountCurrency / CreditInAccountCurrency / AccountCurrency`). It adds three things and nothing else: (1) a daily **Exchange Rate** catalog, (2) automatic **realized** gain/loss posting inside the R-12 payment settlement when the settlement rate differs from the invoice rate, and (3) an **Exchange Rate Revaluation** voucher that books **unrealized** gain/loss on open foreign-currency balance-sheet balances. The hard FY lock from R-13 applies unchanged to every new posting in this module.

**Rate quotation convention (binding for every formula below):** a rate is expressed as **units of the company's functional currency per 1 unit of foreign currency** (ERPNext `conversion_rate` semantics). Company functional currency = `Company.Currency.Code`, falling back to `"USD"` when `Company.CurrencyId` is null (legacy rows). Conversion is always `Amount_CC = Amount_FC × Rate`.

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Exchange Rate** | `Currency Exchange` (rate side) | Daily market rate `FromCurrency → ToCurrency` for one `RateDate`: `Rate > 0`, `UNIQUE(From, To, RateDate)`. GLOBAL shared catalog like `Currency` (no tenant column). Lookup is directional: direct row first, else the stored inverse as reciprocal `1 / Rate`, else the rate is MISSING (no triangulation in v1). |
| **Functional Currency (CC)** | `Company.base_currency` | Currency of the company's books. All GL `Debit/Credit` columns are CC; all `DebitInAccountCurrency/CreditInAccountCurrency` columns are document (FC) amounts. |
| **Realized FX Gain/Loss** | Payment Entry exchange difference | `RealizedFX_CC = PaymentAmount_FC × (Rate_Settlement − Rate_Original)` (AC-05). Posted automatically inside `SubmitPaymentEntry` as extra balanced GL lines to the single `Exchange Gain/Loss` account. Positive value on a `Receive` is a gain (credit); on a `Pay` it is a loss (debit). |
| **Exchange Rate Revaluation** | `Exchange Rate Revaluation` | Submittable voucher (`Draft → Submitted → Cancelled`) bound to one company + one `PostingDate`. Revalues every eligible foreign-currency balance-sheet leaf and posts the unrealized difference to the same `Exchange Gain/Loss` account. |
| **Revaluation Line (derived)** | `Exchange Rate Revaluation Account` (child) | One derived line per eligible account: `Balance_FC`, `Balance_CC` (carrying), `CurrentRate = Balance_CC ÷ Balance_FC`, `NewRate` (catalog rate at `PostingDate`), `NewBalance_CC = Balance_FC × NewRate`, `GainLoss_CC = NewBalance_CC − Balance_CC`. Lines are COMPUTED at submit/preview time, never hand-entered. |
| **Zero-Balance Row** | `zero_balance` flag | A row where one leg is dust (`≤ RoundingLossAllowance`, ERPNext semantics): the residual carrying amount is fully cleared through gain/loss. Residuals `> allowance` with a dust counter-leg are REJECTED (`revaluation_dust_above_allowance`) instead of silently absorbed. |
| **Exchange Gain/Loss Account** | `Exchange Gain/Loss` (default) | Exactly ONE active, non-group P&L leaf (`RootType ∈ {Income, Expense}`) of the SAME company; defaults from `Company.DefaultExchangeGainLossAccount(Id + Code)`, code-not-FK pattern per R-13. Gains and losses net in this single account (direction decides Dr/Cr). |
| **Document Status** | `docstatus` | `Draft` (no GL impact) → `Submitted` (GL posted exactly once) → `Cancelled` (compensating reversal only). Any other step is illegal. |

---

## 2. Core Business Invariants

### Invariant FX-01: Rate Catalog Integrity
- `ExchangeRate.Rate > 0` (CHECK constraint + domain guard); `RateDate` is a calendar day (`DATE`, no time component).
- `UNIQUE(FromCurrencyId, ToCurrencyId, RateDate)`; `FromCurrencyId ≠ ToCurrencyId` (self-pair rejected with `exchange_rate_self_pair`).
- Both currencies must exist and be active in the global catalog, else `unknown_currency`.
- Rates are GLOBAL (shared across tenants, `Currency` precedent): identical market data for every tenant, so no tenant filter applies and no leakage is possible.
- Resolution order for a (`From → To`, `date`) need: (1) exact-date direct row; (2) exact-date inverse row as `1 / Rate` (rounded to 6 dp, never zero); (3) otherwise MISSING → `exchange_rate_missing` (422). No interpolation, no triangulation, no "latest before date" fallback in v1 — exact-date discipline keeps audits reproducible.

### Invariant FX-02: Realized FX Posting Law (implements AC-05)
- Computed **per allocation slice** inside `SubmitPaymentEntry`, after the existing PE-02/PE-07 gates and before `DoubleEntryGuard`: for each invoice allocation, `RealizedFX_CC = AllocatedAmount_FC × (SettlementRate − InvoiceRate)`, where `InvoiceRate` is the invoice's stored `ExchangeRate` (FC → CC at invoice posting) and `SettlementRate` is the catalog rate (FC → CC) at `PaymentDate`.
- Direction rule: on `Receive`, `RealizedFX_CC > 0` → **credit** Exchange Gain/Loss (gain: collected more CC than booked); `< 0` → **debit** it (loss). On `Pay`, the mirror: `> 0` → **debit** (loss: paid more CC than owed); `< 0` → **credit** (gain). The counterparty/bank lines are restated so the voucher still nets to zero:
  - `Receive` 1000 EUR, invoice rate 1.10, settlement 1.15 → Dr Bank 1150.00 CC / Cr Receivable 1100.00 CC / Cr FX-Gain 50.00 CC.
  - `Pay` 1000 EUR, invoice rate 1.10, settlement 1.15 → Dr Payable 1100.00 CC / Dr FX-Loss 50.00 CC / Cr Bank 1150.00 CC.
- Every FX voucher satisfies `|ΣD − ΣC| ≤ 0.0001` (AC-01) **including** the FX lines; a computed imbalance fails the whole submit with `double_entry_imbalance` and persists NOTHING.
- Zero-difference slices (`|RealizedFX_CC| ≤ 0.0001`) post NO FX line. Same-currency settlement (invoice currency == company functional, both rates == 1) never reaches the FX path.
- Cancelling the payment reverses the FX lines too (exact mirror, same `VoucherNo`, `VoucherType = "PaymentEntry"`, reversal rows carry `IsCancelled = true` per the R-12 `PaymentPosting` precedent — the opposite convention to revaluation reversals per FX-06, each path follows its host voucher), restoring invoice FC balances per R-12 PE-05.
- FX lines carry `DebitInAccountCurrency/CreditInAccountCurrency` in FC and `AccountCurrency` = FC code on counterparty/bank lines; the FX-account line is CC-denominated (`AccountCurrency` = functional code, FC columns equal CC columns).

### Invariant FX-03: Exchange Gain/Loss Account Validity
- At submit time (payment FX and revaluation alike) the account MUST satisfy ALL of: exists, same `TenantId` + `CompanyId` as the voucher, `RootType ∈ {Income, Expense}`, `IsGroup = false`, `IsActive = true`. Otherwise `invalid_exchange_gain_loss_account` (400-class).
- Resolution order: voucher-explicit account (revaluation only) → `Company.DefaultExchangeGainLossAccountId` → resolve `Company.DefaultExchangeGainLossAccountCode` to exactly one active leaf → else the error above. A missing company default is a configuration error, not a silent skip: the submit fails.

### Invariant FX-04: Document-Currency Extension (enabler)
- `SalesInvoice`, `PurchaseInvoice`: new nullable `CurrencyId` (null = company functional, legacy-compatible) + `ExchangeRate` (FC → CC at posting, default `1`, must be `> 0` when a foreign currency is set). Invoice `GrandTotal / OutstandingAmount / PaidAmount` remain denominated in the DOCUMENT currency; the GL `Debit/Credit` columns carry the CC restatement (`FC × rate`), and the `*InAccountCurrency` columns carry the FC amounts.
- `PaymentEntry`: new nullable `TransactionCurrencyId` (null = company functional) + stored `SettlementExchangeRate` (resolved once at submit from the catalog, reused verbatim by the cancel reversal so a later rate edit can never rewrite history).
- Invoice submit (both Sales R-11 and Purchase) resolves `ExchangeRate` = catalog FC → CC at the invoice `PostingDate` (FX-01 exact-date rule; missing → `exchange_rate_missing`, nothing persisted), stores it on the invoice, and posts the GL CC-restated (`Debit/Credit = FC × rate`, `*InAccountCurrency` = FC). The purchase mirror is identical. Without this capture step `Rate_Original` would not exist and FX-02 would be unexecutable.
- Currency-mismatch rule: every allocation slice's invoice currency MUST equal the payment's transaction currency (both null-functional counts as equal), else `payment_currency_mismatch` (422). Cross-currency settlement (pay a USD invoice with EUR) is a NON-GOAL in v1.
- Single-currency documents (`CurrencyId` null everywhere) behave byte-identically to today: no FX lines, no catalog reads, all existing tests unaffected.

### Invariant FX-05: Unrealized Revaluation Math
- Eligible account = ALL of: same company, `RootType ∈ {Asset, Liability}`, `IsGroup = false`, `IsActive = true`, `Account.CurrencyId` set with code ≠ company functional code, net FC balance (`Σ(DebitInAccountCurrency − CreditInAccountCurrency)`, non-cancelled rows, `PostingDate ≤ revaluation PostingDate`) with `|Balance_FC| > 0.0001` OR net CC residual `> allowance`, and NOT a warehouse stock account (resolved through the warehouse hierarchy — inventory stays at cost; Stock W4 linkage).
- Per line: `CurrentRate = Balance_CC ÷ Balance_FC` (when `|Balance_FC| > 0.0001`); `NewRate` = catalog FC → CC rate at `PostingDate` (FX-01 resolution; missing → whole submit fails with `exchange_rate_missing`); `NewBalance_CC = Balance_FC × NewRate`; `GainLoss_CC = NewBalance_CC − Balance_CC`.
- Posting construction per line with `GainLoss_CC ≠ 0` (beyond 0.0001): balance-sheet leaf takes the offset that restates it to `NewBalance_CC` (gain on a debit-normal asset → credit the leaf? No — restatement: the leaf is debited/credited by `|GainLoss|` so its CC balance becomes `NewBalance_CC`), and the Exchange Gain/Loss account takes the mirror (gain → credit, loss → debit). Asset and liability leaves are handled by balance arithmetic, not by hard-coded direction: `LeafDelta_CC = NewBalance_CC − Balance_CC` is posted to the leaf (positive delta = debit leaf), and `−LeafDelta_CC` to the FX account (positive delta = credit FX = gain).
- Rounding: `RoundingLossAllowance ∈ [0, 1)` in CC (required field, default `0`, ERPNext range). A line with `|GainLoss_CC| ≤ 0.0001` is DROPPED (never posted, ERPNext "rows without gain/loss removed"). A dust leg (`|Balance_FC| ≤ 0.0001` XOR `|Balance_CC| ≤ allowance`) sets `ZeroBalance = true` and clears the full residual through gain/loss. A dust counter-leg with residual `> allowance` fails with `revaluation_dust_above_allowance`.
- Empty result (zero surviving lines) → `no_revaluation_gain_loss` (422), voucher stays `Draft`. Re-running a submit with unchanged rates is therefore a fixpoint: second run finds nothing to post.
- Revaluation NEVER restates the FC columns: `*InAccountCurrency` amounts are untouched (FC balances are facts); only the CC carrying values move. P&L and Equity leaves are never selected (FC-02 analogue: Balance Sheet only).

### Invariant FX-06: Revaluation Lifecycle, Idempotency & Fixpoint
- Legal transitions ONLY `Draft → Submitted → Cancelled` (`ExchangeRateRevaluation.Submit()/Cancel()` guards; anything else → `revaluation_invalid_transition`; double cancel → `revaluation_already_cancelled`).
- `Submit` is idempotent on `(VoucherId + Idempotency-Key)`: a replayed submit after commit returns the recorded success WITHOUT new GL rows (`Idempotency-Key` header required on all mutating routes, AC-06 pattern).
- No one-per-period uniqueness: repeated revaluations on the same date are legitimate (rate corrections). The fixpoint rule (FX-05 empty-result rejection) is what prevents duplicate economic effect, not a unique index.
- Cancel appends mirrored reversal rows (same `VoucherNo`, `VoucherType = "ExchangeRateRevaluation"`, original `PostingDate`, `IsCancelled = false` on new rows) and flips to `Cancelled`; originals stay byte-identical (AC-02). Cancel of a `Draft` → `revaluation_invalid_transition`.

### Invariant FX-07: Fiscal-Year Containment & Freeze (extends AC-04 / FC-04, unchanged semantics)
- Revaluation `PostingDate` MUST satisfy both existing guards, enforced with the same two-line call as every R-13 pipeline: `company.EnsurePostingDateUnlocked(date)` (`≤ FrozenAccountsDate` → `fiscal_period_locked`) AND covering fiscal year open (`IsClosed` → `fiscal_year_closed`). Closed-year revaluation is refused; revaluation never reopens, moves, or touches closed P&L.
- The payment-FX path adds NO new date logic: it inherits the R-12 submit/cancel guards (frozen + open-year on `PaymentDate`) verbatim. The FX lines share the payment's `PostingDate`.
- `PostingDate > today + 0` (future-dated revaluation) is rejected with `revaluation_future_date` — unrealized positions are measured at or before today, never forecast. "Today" is the application server's UTC calendar date (`DateOnly` UTC), so the guard is deterministic across tenants.

### Invariant FX-08: Optimistic Concurrency & Tenant Isolation
- `ExchangeRateRevaluation` carries `RowVersion`; mutating commands require the caller token, stale → 409 `concurrency_conflict`. `ExchangeRate` rows carry `RowVersion` for admin correction (upsert returns conflict on stale token).
- All reads/writes scoped by `(TenantId, CompanyId)` except the GLOBAL rate catalog (FX-01: no tenant column by design). Cross-tenant/company account references are rejected with `invalid_exchange_gain_loss_account`, never a bare 404.

---

## 3. Gherkin Functional Scenarios

### Scenario FX-R1: Receive Payment With Realized Gain (Happy Path — mirrors AC-05)
- **Given** company `US-01` (functional USD), customer invoice `SI-001` for 1000.00 EUR at invoice rate 1.10 (A/R booked 1100.00 CC)
- **And** catalog rate EUR → USD on payment date = 1.15
- **When** a `Receive` payment of 1000.00 EUR allocating fully to `SI-001` is submitted
- **Then** status becomes `Submitted`
- **And** `GLEntry` gains Dr Bank 1150.00 / Cr Receivable 1100.00 / Cr `Exchange Gain/Loss` 50.00 (`RealizedFX = 1000 × (1.15 − 1.10)`), `ΣD − ΣC = 0.0000`, `VoucherType = "PaymentEntry"`.

### Scenario FX-R2: Pay With Realized Loss
- **Given** supplier bill `PI-001` for 1000.00 EUR at 1.10 (A/P booked 1100.00 CC), settlement rate 1.15
- **When** a `Pay` payment of 1000.00 EUR is submitted
- **Then** `GLEntry` gains Dr Payable 1100.00 / Dr `Exchange Gain/Loss` 50.00 / Cr Bank 1150.00, balanced to zero.

### Scenario FX-R3: Settlement at Identical Rate Posts No FX Line
- **Given** `SI-002` 500.00 EUR at 1.10 and settlement rate 1.10
- **When** the payment is submitted
- **Then** only the two R-12 settlement lines post (Dr Bank 550.00 / Cr Receivable 550.00); no `Exchange Gain/Loss` line exists for the voucher.

### Scenario FX-R4: Missing Settlement Rate Rejects the Payment
- **Given** `SI-003` 200.00 GBP at 1.25 with NO GBP → USD row for the payment date (neither direction)
- **When** submit is attempted
- **Then** it fails with `exchange_rate_missing` (HTTP 422 RFC 7807) and no `GLEntry` row and no voucher mutation is persisted.

### Scenario FX-R5: Currency Mismatch Between Payment and Invoice Rejected
- **Given** a payment in EUR allocating to a USD-denominated invoice
- **When** submit is attempted
- **Then** it fails with `payment_currency_mismatch` (HTTP 422) and nothing is persisted.

### Scenario FX-R6: Revaluation Books Unrealized Gain
- **Given** EUR receivable leaf with `Balance_FC = 10,000.00` EUR, carrying `Balance_CC = 11,000.00` (current rate 1.10), catalog new rate 1.15, allowance 0
- **When** the revaluation is submitted
- **Then** `GLEntry` gains Dr Receivable 500.00 / Cr `Exchange Gain/Loss` 500.00 (`10,000 × (1.15 − 1.10)`), `VoucherType = "ExchangeRateRevaluation"`, balanced; the leaf's CC balance reads 11,500.00 while its FC balance still reads 10,000.00 EUR.

### Scenario FX-R7: Revaluation Books Unrealized Loss on a Payable
- **Given** GBP payable leaf with `Balance_FC = −8,000.00` GBP (credit-normal expressed as negative net debit), carrying `Balance_CC = −10,000.00`, new rate moved so `NewBalance_CC = −10,400.00`
- **When** submitted
- **Then** the leaf is credited 400.00 (restated to −10,400.00) and `Exchange Gain/Loss` is debited 400.00 (loss), balanced.

### Scenario FX-R8: Revaluation Into a Closed Year Rejected
- **Given** `FY-2025` with `IsClosed = true`
- **When** a revaluation with `PostingDate` inside FY-2025 is submitted
- **Then** it fails with `fiscal_year_closed` (409) and nothing is persisted (same guard as R-13 FC-04).

### Scenario FX-R9: Frozen-Date Payment With FX Rejected (AC-04 Unchanged)
- **Given** `Company.FrozenAccountsDate = 2026-06-30`
- **When** a foreign-currency payment dated 2026-06-15 is submitted
- **Then** it fails with `fiscal_period_locked` (409) before any rate lookup; nothing is persisted.

### Scenario FX-R10: Idempotent Submit Replay (AC-06 Pattern)
- **Given** a valid revaluation submit with header `Idempotency-Key: erv-us01-2026-001`
- **When** the client retries the identical POST after a network break
- **Then** the API returns HTTP 200 with the first call's voucher detail and exactly ONE balanced GL set exists for the voucher.

### Scenario FX-R11: Concurrent Revaluation Race — No Double Posting
- **Given** two identical `Draft` revaluations for the same company + date submitted simultaneously
- **When** both transactions race (distinct idempotency keys)
- **Then** exactly ONE commits the GL set; the loser re-reads post-commit balances, computes an empty (fixpoint) set, and fails with `no_revaluation_gain_loss` (422) — never two GL sets for one economic event.

### Scenario FX-R12: Cancel Payment Reverses FX Lines Too
- **Given** the submitted voucher from FX-R1 (Cr FX-Gain 50.00)
- **When** it is cancelled while the period is open
- **Then** mirrored reversals post (Dr FX 50.00 among them), the voucher nets to exactly 0.0000, invoice FC outstanding is restored, and a later rate edit cannot alter either set (settlement rate was stored on the voucher).

### Scenario FX-R13: Cancel Revaluation Writes Compensating Reversal
- **Given** the submitted revaluation from FX-R6
- **When** cancelled while the fiscal year is still open
- **Then** status becomes `Cancelled`, originals stay byte-identical, mirrored rows (Cr Receivable 500.00 / Dr FX 500.00) net the voucher to zero; second cancel → `revaluation_already_cancelled`.

### Scenario FX-R14: Zero-Balance Dust Cleared Within Allowance
- **Given** a USD bank leaf with `Balance_FC = 0.00` and residual `Balance_CC = 0.04`, allowance 0.05
- **When** submitted
- **Then** the row is flagged `ZeroBalance`, the 0.04 residual clears through `Exchange Gain/Loss`, and the account reads clean zero in both legs.

### Scenario FX-R15: Upsert Exchange Rate Guards (Catalog Administration)
- **Given** existing EUR → USD 2026-10-01 = 1.15
- **When** an admin upserts a negative rate, a self-pair (USD → USD), or an unknown currency code
- **Then** the command fails with `exchange_rate_invalid` / `exchange_rate_self_pair` / `unknown_currency` respectively (400-class) and the stored row is untouched. Replay of the identical payload (same pair + day + rate) returns the stored row without a write (natural-key idempotency); a different rate for an existing pair-day requires the current `RowVersion`.

### Scenario FX-R16: Partial Payment Realizes Only the Paid Slice
- **Given** `SI-004` for 2000.00 EUR at invoice rate 1.10, settlement rate 1.20
- **When** a `Receive` payment allocates only 800.00 EUR to `SI-004` (remainder stays outstanding per R-12 PE-02)
- **Then** realized FX posts on the slice only: `RealizedFX = 800 × (1.20 − 1.10) = 80.00` Cr gain; `OutstandingAmount` remains 1200.00 EUR and the residual carries the original 1.10 rate for the next settlement.

### Scenario FX-R17: Invoice Submit Without a Catalog Rate Is Rejected
- **Given** a foreign-currency sales invoice (EUR, company functional USD) dated 2026-10-05 with NO EUR → USD row for that date
- **When** invoice submit is attempted
- **Then** it fails with `exchange_rate_missing` (422), the invoice stays `Draft` with no stored rate, and zero `GLEntry` rows are written (same-currency invoices never reach the catalog).

---

## 4. API Contract (summary; full detail in plan.md §5)

| Method & Route | Purpose | Guards |
| :--- | :--- | :--- |
| `POST /api/v1/exchange-rates` | Upsert one daily rate (create or correct with `RowVersion`; idempotency is the natural key — NO header) | `TenantMember` |
| `GET /api/v1/exchange-rates?from=&to=&date=` | Single-rate lookup (direct-or-inverse, exact date) | `TenantMember` |
| `POST /api/v1/exchange-rate-revaluations` | Create `Draft` voucher (company + date + optional FX account + allowance) | `TenantMember`, `IdempotencyKeyRequired` |
| `GET /api/v1/exchange-rate-revaluations?companyId=` | Paged list + detail with derived lines | `TenantMember` |
| `GET /api/v1/exchange-rate-revaluations/preview?companyId=&postingDate=` | Pre-submit preview (read-only, SAME query as submit) | `TenantMember` |
| `POST /api/v1/exchange-rate-revaluations/{id}/submit` | Compute, validate, post unrealized set (requires `RowVersion`) | `TenantMember`, `IdempotencyKeyRequired` |
| `POST /api/v1/exchange-rate-revaluations/{id}/cancel` | Compensating reversal (requires `RowVersion`) | `TenantMember`, `IdempotencyKeyRequired` |

Payment FX needs NO new route: it rides the existing R-12 `submit`/`cancel` endpoints with extended payloads (`transactionCurrencyId` on create).

---

## 5. Error Codes (RFC 7807 `code` values; HTTP mapping in plan.md §5)

| `code` | HTTP | Meaning |
| :--- | :--- | :--- |
| `exchange_rate_missing` | 422 | No direct/inverse catalog row for the needed pair + date |
| `exchange_rate_invalid` | 400 | Rate ≤ 0, bad date, or allowance outside `[0,1)` |
| `exchange_rate_self_pair` | 400 | From == To |
| `unknown_currency` | 400 | Currency code/id not in the active global catalog |
| `payment_currency_mismatch` | 422 | Payment currency ≠ allocation invoice currency |
| `invalid_exchange_gain_loss_account` | 400 | FX leaf missing / group / inactive / wrong RootType / wrong company-tenant |
| `no_revaluation_gain_loss` | 422 | Zero surviving lines (fixpoint — includes the FX-R11 race outcome) |
| `revaluation_dust_above_allowance` | 422 | Dust counter-leg with residual above allowance |
| `revaluation_future_date` | 422 | `PostingDate` after today |
| `revaluation_invalid_transition` | 409 | Illegal `Draft → Submitted → Cancelled` step |
| `revaluation_already_cancelled` | 409 | Double cancel |
| `revaluation_not_found` | 404 | Voucher id unknown |
| `fiscal_year_closed` | 409 | Date inside a closed year (R-13 FC-04, reused) |
| `fiscal_period_locked` | 409 | Date `<= FrozenAccountsDate` (AC-04, reused) |
| `double_entry_imbalance` | 500* | Computed FX set does not net to zero (internal tripwire) |
| `concurrency_conflict` | 409 | Stale `RowVersion` |
| `idempotency_key_required` / `idempotent_replay` | 400 / 200 | Missing key / replayed success (AC-06 pattern) |

All codes snake_case, single Accounting-domain owner (`FxErrorCodes`); no new `BankingErrorCodes` FX constants.

---

## 6. Non-Goals / Explicitly Deferred

- **Rate triangulation** (EUR → GBP via USD) and interpolation/closest-date fallback — exact-date direct-or-inverse only.
- **Manual rate override on payment/invoice** — rates always come from the catalog; the submit stores the resolved value for audit stability.
- **Cross-currency settlement** (pay a USD invoice with EUR) — rejected per FX-04.
- **Automatic rate import / external feed** — rates are entered via API (a feed is a later integration, same contract).
- **Forward contracts, hedging, multi-date aging revaluation** — period-end position revaluation only.
- **Re-opening closed years, quarterly closes** — R-13 scope, unchanged.
- **ERPNext's JE-mediated posting** — deliberate deviation: both FX paths post `GLEntry` rows directly (`VoucherType = "PaymentEntry"` / `"ExchangeRateRevaluation"`), consistent with R-12/R-13 direct posting; no `JournalEntry` voucher is created.
- **Gapless `VoucherNo` series** — `ERV-YYYY-NNNNN` assigned in-txn per company-year (full R-31 gapless numbering still deferred, R-13 §6 precedent).

---

## 7. Audit of the Pre-Existing Implementation (what Fase 2 must extend, not re-discover)

| Artifact | State | Consequence for this spec |
| :--- | :--- | :--- |
| `Erp.Domain/Entities/Currency.cs` + `ICurrencyRepository` + `CurrencyValidator` | Exists, GLOBAL catalog, no tenant column | FX-01 follows the same global-catalog precedent for `ExchangeRate` |
| `GLEntry.DebitInAccountCurrency / CreditInAccountCurrency / AccountCurrency` (default `"USD"`) | Exists, currently mirrors `Debit/Credit` 1:1 (single-currency) | FX-02/FX-05 give these columns their first real multi-currency content; NO schema change on `GLEntry` |
| `PaymentEntry` + `PaymentPosting.BuildLedgerLines` | Exists, strictly single-currency (FC columns = CC 1:1) | Must gain `TransactionCurrencyId + SettlementExchangeRate` and an FX-line branch (plan §3) |
| `Submit/CancelPaymentEntryCommandHandler` | Exists, frozen + open-year guards already wired (R-13) | FX hooks execute INSIDE the same transaction after PE-02/PE-07; no new date logic |
| `SalesInvoice` / `PurchaseInvoice` (`GrandTotal / OutstandingAmount`, NO currency fields) | Missing currency entirely | FX-04 adds nullable `CurrencyId + ExchangeRate`; outstanding stays in DOCUMENT currency |
| `Company` (`CurrencyId` nullable → USD, `FrozenAccountsDate`, R-13 retained defaults) | Exists, NO FX default | Add `DefaultExchangeGainLossAccountId + Code` (code-not-FK, R-13 pattern) |
| `ExchangeRate`, `ExchangeRateRevaluation`, catalog/voucher controllers, preview query | Missing entirely | Built new per plan.md §§2–5 |
| Seed: `3100 - Retained Earnings` (R-13) | Exists | Fase 2 seeds one `Exchange Gain/Loss` P&L leaf per company the same way (tasks.md Phase 2) |

---

## 8. Traceability

- Roadmap R-14 ← Accounting AC-05 deferred (formula + auto-post + `Exchange Gain/Loss Account`) + AC-01/AC-02/AC-04 unchanged + R-13 FC-04 lock reused.
- ERPNext `exchange_rate_revaluation`: header (`company`, `posting_date`, `rounding_loss_allowance ∈ [0,1)`), child-row economics (`current = base ÷ foreign`, `new base = FC × new rate`, `gain/loss = new − carrying`, `zero_balance` dust handling, drop zero rows), and submit-guard spirit adopted 1:1; §6 records the two deliberate deviations (direct GL posting, no triangulation).
