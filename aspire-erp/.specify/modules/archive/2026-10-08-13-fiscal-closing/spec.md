# Functional Specification: Fiscal Year + Period Closing Voucher (ERPNext Parity)

**Module:** `13-fiscal-closing`
**Status:** DRAFT — pending review (roadmap.md row **R-13**; depends on R-12)
**Version:** 1.0.0
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit
**Canonical Reference:** ERPNext `erpnext/accounts/doctype/period_closing_voucher` (verified per roadmap §4) + `fiscal_year` boundary semantics
**Closes:** Accounting AC-06 deferred (spec `01-accounting` §2 invariant AC-06 + scenario AC-05); hardens AC-04 (freeze-date lock) into every posting pipeline

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

---

## 1. Executive Summary & Ubiquitous Language

The **Fiscal Closing** module gives Aspire ERP a real financial calendar. A **Fiscal Year** bounds one accounting year per company; a **Period Closing Voucher** zeroes every Profit & Loss leaf (Income + Expense) inside that year and transfers the net result to a single **Retained Earnings** Equity leaf in one atomic, balanced, idempotent posting. A **hard period lock** then guarantees nothing — no invoice, payment, journal, stock posting, nor a second closing — can ever post into a closed year or on/before `Company.FrozenAccountsDate`.

This change audits and completes the partial implementation already present in the repo (see §7) instead of green-fielding it.

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Fiscal Year** | `Fiscal Year` | Named year boundary per company: `YearName`, `StartDate < EndDate`, `IsClosed` flag. `UNIQUE(CompanyId, YearName)`. Closing sets `IsClosed = 1`; re-open is FORBIDDEN in this change (manual DBA escape hatch only, out of scope). |
| **Period Closing Voucher** | `Period Closing Voucher` | Submittable voucher (`Draft → Submitted → Cancelled`) bound to exactly one open Fiscal Year. `PostingDate` MUST fall inside `[FiscalYear.StartDate, FiscalYear.EndDate]`. Posts to the GL exactly once, on submit. |
| **Closing Lines (derived)** | `Period Closing Voucher Details` (child table) | One derived line per P&L leaf with non-zero balance in the fiscal year: Income leaves are debited to zero, Expense leaves are credited to zero. Lines are COMPUTED at submit time from live GL balances, never hand-entered. |
| **Retained Earnings Account** | `Account` (Equity leaf) | Exactly one active, non-group, `RootType = Equity` leaf of the SAME company (defaults from `Company.DefaultRetainedEarningsAccountId`). Receives the net P&L: credit on profit, debit on loss, zero-line on break-even. |
| **Net P&L** | — | `Net = Σ IncomeBalances − Σ ExpenseBalances` where each balance = `ΣCredit − ΣDebit` (credit-normal) over non-cancelled GL rows in the fiscal year window. `Net > 0` (profit) → credit retained; `Net < 0` (loss) → debit retained; `Net = 0` → balanced zero-net close still zeroes every leaf. |
| **Hard Period Lock** | `Accounts Settings.freeze_date` + `Fiscal Year.is_closed` | Two-layer guard enforced at EVERY posting entry point via `Company.EnsurePostingDateUnlocked` + new `FiscalYear.EnsurePostingAllowed`: reject when `postingDate <= FrozenAccountsDate` OR when the date falls in a closed fiscal year. |
| **Document Status** | `docstatus` | `Draft` (no GL impact) → `Submitted` (GL closed, exactly once) → `Cancelled` (compensating reversal only, never mutation). `Submitted → Draft` and `Cancelled → *` are illegal. |

---

## 2. Core Business Invariants

### Invariant FC-01: Double-Entry Closure Law (extends AC-01)
Every submit appends a voucher whose lines net to exactly zero:

$$\left| \sum \text{Debit} - \sum \text{Credit} \right| \le 0.0001$$

Construction: for each P&L leaf with credit-normal balance `b ≠ 0`, post `Debit = max(b,0)` / `Credit = max(−b,0)` on that leaf (zeroing it), and post the mirror on retained earnings (`Debit = max(−Net,0)` / `Credit = max(Net,0)`). A submit whose computed set violates the equality fails with `double_entry_imbalance` and persists NOTHING.

### Invariant FC-02: P&L-Only Closure (Balance Sheet Untouched)
Only accounts with `RootType ∈ {Income, Expense}`, `IsGroup = false`, `IsActive = true`, belonging to the voucher's company, and with non-zero balance inside the fiscal-year window are closed. Asset / Liability / Equity leaves (including retained earnings itself) NEVER receive a zeroing line. If a computed line targets a non-P&L account the submit fails with `closing_non_pl_account`.

### Invariant FC-03: Retained Earnings Validity
At submit time the retained account MUST satisfy ALL of: exists, `TenantId` + `CompanyId` match the voucher, `RootType = Equity`, `IsGroup = false`, `IsActive = true`. Otherwise `invalid_retained_earnings_account` (400-class). The voucher's explicit `RetainedEarningsAccountId` wins; when null, `Company.DefaultRetainedEarningsAccountId` is used; when both null → same error.

### Invariant FC-04: Fiscal-Year Containment (extends AC-04)
- `FiscalYear.StartDate < FiscalYear.EndDate` (CHECK constraint + domain guard).
- `UNIQUE(CompanyId, YearName)`; overlapping date ranges for the same company are REJECTED with `fiscal_year_overlap`.
- Voucher `PostingDate ∈ [StartDate, EndDate]` of its fiscal year, else `closing_date_outside_fiscal_year`.
- Submit/cancel against a fiscal year with `IsClosed = true` → `fiscal_year_closed` (409).
- `PostingDate <= Company.FrozenAccountsDate` → `fiscal_period_locked` (existing AC-04, 409).

### Invariant FC-05: Lifecycle, Idempotency & Single Posting
- Legal transitions ONLY `Draft → Submitted → Cancelled`. Anything else → `period_closing_invalid_transition`.
- Exactly ONE submitted (non-cancelled) closing voucher per `(CompanyId, FiscalYearId)` — enforced by a filtered unique index + application guard (`duplicate_closing_for_fiscal_year`).
- `Submit` is idempotent on `(VoucherId + Idempotency-Key)`: a replayed submit after commit returns the recorded success WITHOUT new GL rows (`Idempotency-Key` header required on all mutating routes).
- Double submit of a `Submitted` voucher → `period_closing_invalid_transition`; double cancel of a `Cancelled` voucher → `period_closing_already_cancelled`.
- The close is year-scoped: only GL rows with `PostingDate ∈ [StartDate, EndDate]`, `IsCancelled = false`, and NOT produced by an already-submitted closing voucher for the same year participate. Re-running the balance query after a successful submit yields the empty set (close is a fixpoint).

### Invariant FC-06: Cancellation by Compensating Reversal (extends AC-02)
Cancelling a `Submitted` voucher NEVER sets `GLEntry.IsCancelled = true` in place and never deletes rows. It appends mirrored lines (Debit/Credit swapped, same `VoucherNo`, `VoucherType = "PeriodClosingVoucher"`, original `PostingDate`, `IsCancelled = false` on the new rows, remarks prefixed `Reversal of …`), flips the voucher to `Cancelled`, and leaves the fiscal year OPEN (re-close requires a new voucher). Cancel of a `Draft` → `period_closing_invalid_transition`. Cancel while the fiscal year is closed → `fiscal_year_closed` (a closed year is immutable; re-open is out of scope).

### Invariant FC-07: Optimistic Concurrency & Tenant Isolation
- Every `FiscalYear` and `PeriodClosingVoucher` carries `RowVersion`; mutating commands require the caller-supplied `RowVersion` and a stale token yields 409 `concurrency_conflict`.
- All reads/writes are scoped by `(TenantId, CompanyId)`; cross-tenant/company account references are rejected with the same `invalid_retained_earnings_account` / `closing_non_pl_account` codes (never a bare 404 that leaks existence).

---

## 3. Gherkin Functional Scenarios

### Scenario FC-01: Year-End Close With Profit (Happy Path — mirrors AC-05)
- **Given** Fiscal Year `FY-2025` (`2025-01-01 → 2025-12-31`, open) for company `US-01`
- **And** P&L balances in FY-2025: Revenue `4000` credit-normal `500,000.00`, Expenses `5000` debit-normal `380,000.00` (Net `+120,000.00`)
- **And** retained leaf `3100 - Retained Earnings` (Equity, active, same company)
- **When** the accountant submits a `Draft` closing voucher with `PostingDate = 2025-12-31`
- **Then** status becomes `Submitted`
- **And** `GLEntry` gains: Dr `4000` `500,000.00`, Cr `5000` `380,000.00`, Cr `3100` `120,000.00` with `VoucherType = "PeriodClosingVoucher"`, `ΣD − ΣC = 0.0000`
- **And** every P&L leaf balance in FY-2025 reads `0.00` afterwards.

### Scenario FC-02: Close With Net Loss
- **Given** FY-2025 with Revenue `200,000.00`, Expenses `260,000.00` (Net `−60,000.00`)
- **When** the voucher is submitted
- **Then** `GLEntry` gains Dr `4000` `200,000.00`, Cr `5000` `260,000.00`, Dr `3100` `60,000.00`, balanced to zero.

### Scenario FC-03: Break-Even Close (Net Zero Still Zeroes Leaves)
- **Given** FY-2025 with Revenue `150,000.00` and Expenses `150,000.00` (Net `0.00`)
- **When** the voucher is submitted
- **Then** it succeeds with Dr/Cr pairs zeroing each leaf and a zero-amount marker is NOT posted to retained earnings (no zero-value GL row); the voucher is `Submitted` and the year can still be hard-locked.

### Scenario FC-04: Reject Submit Into Closed Fiscal Year
- **Given** `FY-2025` with `IsClosed = true`
- **When** any create/submit/cancel targets FY-2025
- **Then** the command fails with `fiscal_year_closed` (HTTP 409 RFC 7807)
- **And** no `GLEntry` row and no voucher mutation is persisted.

### Scenario FC-05: Reject Invalid Retained Earnings Account
- **Given** a voucher naming account `1000 - Assets` (group), `4000 - Revenue` (Income, not Equity), an inactive leaf, or a leaf of another company
- **When** submit is attempted
- **Then** it fails with `invalid_retained_earnings_account` (HTTP 400)
- **And** nothing is persisted.

### Scenario FC-06: Reject Non-P&L Contamination
- **Given** GL activity on Asset `1110 - Cash` inside FY-2025
- **When** the voucher is submitted
- **Then** `1110` receives NO zeroing line (Balance Sheet intact)
- **And** any engine defect that would target it fails the whole transaction with `closing_non_pl_account`.

### Scenario FC-07: Reject Frozen-Date Posting (AC-04 Hardened)
- **Given** `Company.FrozenAccountsDate = 2025-12-31`
- **When** a closing voucher with `PostingDate = 2025-12-15` is submitted (or any GL posts on/before the freeze date)
- **Then** the command fails with `fiscal_period_locked` (HTTP 409)
- **And** no data is modified.

### Scenario FC-08: Reject Date Outside Fiscal Year
- **Given** `FY-2025` (`2025-01-01 → 2025-12-31`)
- **When** a voucher with `PostingDate = 2026-01-05` claims FY-2025
- **Then** it fails with `closing_date_outside_fiscal_year` (HTTP 422)
- **And** nothing is persisted.

### Scenario FC-09: Idempotent Submit Replay (AC-06 Pattern)
- **Given** a valid submit with header `Idempotency-Key: close-fy2025-us01-001`
- **When** the client retries the identical POST after a network break
- **Then** the API returns HTTP 200 with the first call's voucher detail
- **And** exactly ONE balanced GL set exists for the voucher.

### Scenario FC-10: Concurrent Submit Race — Exactly One Winner
- **Given** two clerks submit different `Draft` vouchers for the same open FY-2025 simultaneously
- **When** both transactions race
- **Then** exactly ONE commits (`Submitted`); the loser gets `duplicate_closing_for_fiscal_year` (409) or `concurrency_conflict` after re-validation — never two GL sets for one year.

### Scenario FC-11: Cancel Writes Compensating Reversal (Not Mutation)
- **Given** the submitted voucher from FC-01 (Cr `3100` `120,000.00`)
- **When** the supervisor cancels it while FY-2025 is still open
- **Then** status becomes `Cancelled`, the original rows stay byte-identical (`IsCancelled = false` preserved), and mirrored reversal rows (Dr `3100` `120,000.00`, Cr `4000` `500,000.00`, Dr `5000` `380,000.00`) net the voucher to exactly `0.0000`
- **And** cancelling again → `period_closing_already_cancelled`.

### Scenario FC-12: Empty Year Is Rejected, Not Silently Submitted
- **Given** FY-2026 with zero P&L balances
- **When** submit is attempted
- **Then** it fails with `no_closing_balances` (HTTP 422) and the voucher stays `Draft`.

### Scenario FC-13: Overlapping Fiscal Year Creation Is Rejected
- **Given** `FY-2025` (`2025-01-01 → 2025-12-31`) exists for company `US-01`
- **When** a user creates `FY-2025b` (`2025-06-01 → 2026-05-31`) for the same company
- **Then** creation fails with `fiscal_year_overlap` (HTTP 409)
- **And** no fiscal-year row is persisted (same-company overlap check; different companies are independent).

### Scenario FC-14: Year Close Refused While a Draft Voucher Is Open
- **Given** open `FY-2025` with a `Draft` closing voucher still pending
- **When** the supervisor runs close-year
- **Then** the command fails with `period_closing_invalid_transition` (HTTP 409)
- **And** the year stays open until the draft is submitted or deleted.

---

## 4. API Contract (summary; full detail in plan.md §5)

| Method & Route | Purpose | Guards |
| :--- | :--- | :--- |
| `POST /api/v1/fiscal-years` | Create open fiscal year (no overlap) | `TenantMember`, `IdempotencyKeyRequired` |
| `GET /api/v1/fiscal-years?companyId=` | Paged list with `IsClosed` filter | `TenantMember` |
| `POST /api/v1/fiscal-years/{id}/close` | Hard-lock the year (`IsClosed = 1`, requires `RowVersion`) | `TenantMember`, `IdempotencyKeyRequired` |
| `POST /api/v1/period-closing-vouchers` | Create `Draft` voucher bound to a fiscal year | `TenantMember`, `IdempotencyKeyRequired` |
| `GET /api/v1/period-closing-vouchers?companyId=&fiscalYearId=` | Paged list + detail with derived P&L preview | `TenantMember` |
| `POST /api/v1/period-closing-vouchers/{id}/submit` | Compute P&L, validate, post balanced close (requires `RowVersion`) | `TenantMember`, `IdempotencyKeyRequired` |
| `POST /api/v1/period-closing-vouchers/{id}/cancel` | Compensating reversal (requires `RowVersion`) | `TenantMember`, `IdempotencyKeyRequired` |
| `GET /api/v1/period-closing-vouchers/unclosed-balances?companyId=&fiscalYearId=` | Pre-submit P&L preview (read-only, same query as submit) | `TenantMember` |

---

## 5. Error Codes (RFC 7807 `code` values; HTTP mapping in plan.md §5)

| `code` | HTTP | Meaning |
| :--- | :--- | :--- |
| `fiscal_year_closed` | 409 | Target fiscal year `IsClosed = true` |
| `fiscal_year_overlap` | 409 | New year overlaps an existing year of the same company |
| `closing_date_outside_fiscal_year` | 422 | `PostingDate ∉ [StartDate, EndDate]` |
| `invalid_retained_earnings_account` | 400 | Retained leaf missing / group / inactive / wrong RootType / wrong company-tenant |
| `closing_non_pl_account` | 500* | Engine attempted to close a non-P&L account (defense-in-depth tripwire; *500 because it signals an internal bug, not caller error) |
| `duplicate_closing_for_fiscal_year` | 409 | A submitted non-cancelled voucher already exists for `(CompanyId, FiscalYearId)` |
| `no_closing_balances` | 422 | Zero P&L balances in the year window |
| `double_entry_imbalance` | 500* | Computed close does not net to zero (internal tripwire) |
| `fiscal_period_locked` | 409 | `PostingDate <= FrozenAccountsDate` (AC-04, existing) |
| `period_closing_not_found` | 404 | Closing voucher id unknown (existing) |
| `fiscal_year_not_found` | 404 | Fiscal year id unknown |
| `period_closing_invalid_transition` | 409 | Illegal `Draft → Submitted → Cancelled` step (existing) |
| `period_closing_already_cancelled` | 409 | Double cancel (existing) |
| `concurrency_conflict` | 409 | Stale `RowVersion` |
| `idempotency_key_required` / `idempotent_replay` | 400 / 200 | Missing key / replayed success (existing AC-06 pattern) |

All legacy `BankingErrorCodes.period_closing_*` constants are superseded by this vocabulary owned by the Accounting domain (single owner, snake_case).

---

## 6. Non-Goals / Explicitly Deferred

- **Re-opening a closed fiscal year** (no `POST …/reopen`; closed is terminal in this change).
- **Partial-period / quarterly closes** (only full fiscal-year scope).
- **Multi-currency FX on close / revaluation** → R-14.
- **Automatic `FrozenAccountsDate` bump on year close** (operator sets it explicitly; the close does not move the freeze date by itself).
- **Advance allocation consumption, tax withholding on close** → R-15 / follow-ups.
- **Naming-series gapless `VoucherNo`** beyond the existing `CompanyId + VoucherNo` unique rule (Constitution III.4 full gapless numbering arrives with R-31; this change assigns `PCV-<year>-<seq>` inside the submit transaction).

---

## 7. Audit of the Pre-Existing Partial Implementation (what Fase 2 must fix, not re-discover)

| Artifact | State | Defect this spec closes |
| :--- | :--- | :--- |
| `Erp.Domain/Entities/PeriodClosingVoucher.cs` | Exists, no `FiscalYearId`, no lines nav, no company FK | FC-04 unmodellable → add `FiscalYearId` + navs + `IdempotencyKey` |
| `SubmitPeriodClosingVoucherCommandHandler.cs` | Naïve: groups P&L, posts offsets, credits retained | No FY check, no frozen check, no retained validation, no balance assertion, no idempotency, no RowVersion, unbounded date window (`<= PostingDate` instead of FY range), no duplicate-year guard, no transaction-scoped `VoucherNo` |
| `CancelPeriodClosingVoucherCommandHandler.cs` | Sets `IsCancelled = true` on originals | Violates append-only AC-02/FC-06 → must append reversals |
| `PeriodClosingVoucherRepository` | `GetUnclosedPLEntriesAsync` unbounded + `UpdateGLEntries` mutation | Must become FY-windowed read + reversal-append; drop the mutating update path |
| Migration `20261008000616_AddPeriodClosingVouchers` | Table without FKs/CHECKs/filtered unique | Replaced by plan.md §2 DDL (FKs, `CK_FY_Dates`, filtered `UQ_One_Submitted_Close_Per_Year`, composite indexes) |
| `PeriodClosingVoucherDto` | No `FiscalYearId`, no `RowVersion` | Extended in plan.md §4 |
| `BankingErrorCodes.period_closing_*` | Codes owned by wrong domain | Moved to Accounting-owned `FiscalClosingErrorCodes` (§5) |
| Frontend `usePeriodClosing.ts` / `PeriodClosingList.tsx` / `PeriodClosingFormModal.tsx` | List + bare create modal, no FY picker, no submit/cancel, no preview, no RowVersion/idempotency | Rebuilt per tasks.md Phase 5 (FiscalYear master + voucher execution screen) |
| **Missing entirely** | `FiscalYear` entity, hard-lock calls in stock/buying/selling/JE pipelines, `CloseFiscalYear` path | Tasks.md Phases 1–2 |

---

## 8. Traceability

- Roadmap R-13 ← Accounting AC-06 deferred + AC-04 hardened + scenario AC-05 (profit 500k/380k/120k to `3100 - Retained Earnings`).
- ERPNext `period_closing_voucher`: `posting_date`, `fiscal_year`, `company`, `remarks`, `docstatus`; P&L → retained earnings; closed-year posting refused. `Fiscal Year` boundary semantics adopted 1:1.
