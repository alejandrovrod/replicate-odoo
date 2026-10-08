# Technical Plan: Multi-Currency FX — Exchange Rates, Realized FX, Revaluation (R-14)

**Module:** `14-fx-revaluation`
**Status:** DRAFT — pending review
**Version:** 1.0.0
**Stack:** .NET 10, SQL Server 2025, EF Core 9, React 19 + TypeScript
**Architectural Standard:** Clean Architecture, DDD, CQRS, Tenant Isolation
**Spec:** [spec.md](./spec.md) · **Tasks:** [tasks.md](./tasks.md)

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**

---

## 1. Clean Architecture Topology & Layers

```
src/Backend/
├── Erp.Domain/
│   ├── Entities/
│   │   ├── ExchangeRate.cs                    # NEW — global daily rate (From/To CurrencyId, RateDate, Rate, RowVersion)
│   │   ├── ExchangeRateValidator.cs           # NEW — pure guards (Rate>0, no self-pair, allowance range)
│   │   ├── ExchangeRateRevaluation.cs         # NEW — Draft→Submitted→Cancelled + Submit()/Cancel() + allowance guard
│   │   ├── ExchangeRateRevaluationLine.cs     # NEW — derived-line snapshot (FC/CC balances, rates, gain/loss, ZeroBalance)
│   │   ├── FxErrorCodes.cs                    # NEW — Accounting-owned snake_case vocabulary (spec §5)
│   │   ├── SalesInvoice.cs                    # EXTEND — + CurrencyId (nullable) + ExchangeRate (default 1)
│   │   ├── PurchaseInvoice.cs                 # EXTEND — + CurrencyId (nullable) + ExchangeRate (default 1)
│   │   ├── PaymentEntry.cs                    # EXTEND — + TransactionCurrencyId (nullable) + SettlementExchangeRate
│   │   ├── Company.cs                         # EXTEND — + DefaultExchangeGainLossAccountId + Code (code-not-FK, R-13 pattern)
│   │   ├── GLEntry.cs                         # UNCHANGED — VoucherType="ExchangeRateRevaluation" rows appended; FC columns gain real content
│   │   └── Exceptions/ (UnknownCurrencyException, ExchangeRateMissingException, PaymentCurrencyMismatchException,
│   │                     InvalidExchangeGainLossAccountException, NoRevaluationGainLossException,
│   │                     RevaluationDustAboveAllowanceException, RevaluationFutureDateException,
│   │                     RevaluationInvalidTransitionException, RevaluationAlreadyCancelledException)
│   │                     — reuse FiscalPeriodLockedException, FiscalYearClosedException, ConcurrencyConflictException
│   ├── Repositories/
│   │   ├── IExchangeRateRepository.cs         # NEW — exact-date direct/inverse lookup, upsert, date-scoped list
│   │   └── IExchangeRateRevaluationRepository.cs  # NEW — voucher CRUD, FY-open-aware submit lock, reversal append
│   ├── Services/
│   │   └── FxCalculator.cs                    # NEW — pure math: realized per-slice, unrealized per-line, balance asserts
├── Erp.Application/
│   ├── Features/Forex/
│   │   ├── UpsertExchangeRateCommand(+Handler+Validator — RowVersion on correct, idempotent)
│   │   ├── GetExchangeRateQuery(+Handler — direct-or-inverse, exact date)
│   │   ├── CreateExchangeRateRevaluationCommand(+Handler+Validator — Draft, FY/frozen/future pre-checks)
│   │   ├── SubmitExchangeRateRevaluationCommand(+Handler — §3 pipeline)
│   │   ├── CancelExchangeRateRevaluationCommand(+Handler — §3 reversal)
│   │   ├── GetExchangeRateRevaluationsQuery + GetExchangeRateRevaluationDetailQuery
│   │   └── GetRevaluationPreviewQuery(+Handler — read-only, SAME query as submit step 3)
│   ├── Features/Payments/ (TOUCH, not rewrite)
│   │   ├── CreatePaymentEntryCommand           # EXTEND payload: + TransactionCurrencyId
│   │   ├── SubmitPaymentEntryCommandHandler    # EXTEND: FX branch after PE-02/PE-07 (§3.2)
│   │   └── CancelPaymentEntryCommandHandler    # EXTEND: reverse stored FX lines (§3.3)
│   └── DTOs/ (ExchangeRateDto, ExchangeRateRevaluationDto, RevaluationLineDto, RevaluationPreviewDto)
├── Erp.Infrastructure/
│   ├── Data/Configurations/ (ExchangeRateConfiguration, ExchangeRateRevaluationConfiguration,
│   │                          ExchangeRateRevaluationLineConfiguration, CompanyConfiguration touch-up,
│   │                          SalesInvoice/PurchaseInvoice/PaymentEntryConfiguration touch-ups)
│   ├── Data/Repositories/ (ExchangeRateRepository, ExchangeRateRevaluationRepository)
│   └── Migrations/ (AddFxRevaluation — §2 DDL)
└── Erp.Api/
    ├── Controllers/V1/ (ExchangeRatesController, ExchangeRateRevaluationsController — §5 routes)
    └── ProblemDetails mapping for every spec §5 code → HTTP status
src/Frontend/erp-client/src/features/accounting/
├── exchange-rates/ (ExchangeRateList.tsx, ExchangeRateFormModal.tsx, useExchangeRates.ts)
└── revaluation/ (RevaluationList.tsx, RevaluationFormModal → execution screen:
                  company/date/allowance pickers, FX-account picker, preview grid, Submit/Cancel
                  with RowVersion + Idempotency-Key)
```

---

## 2. SQL Server 2025 Physical Schema (DDL)

```sql
-- 2.1 Exchange-rate catalog (NEW, GLOBAL — no TenantId, Currency precedent §FX-01)
CREATE TABLE ExchangeRates (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    FromCurrencyId UNIQUEIDENTIFIER NOT NULL,
    ToCurrencyId UNIQUEIDENTIFIER NOT NULL,
    RateDate DATE NOT NULL,
    Rate DECIMAL(18,6) NOT NULL,               -- CC per 1 FC, strictly positive
    RowVersion ROWVERSION NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_EXR_From FOREIGN KEY (FromCurrencyId) REFERENCES Currencies(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_EXR_To FOREIGN KEY (ToCurrencyId) REFERENCES Currencies(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_EXR_Rate CHECK (Rate > 0),
    CONSTRAINT CK_EXR_NoSelfPair CHECK (FromCurrencyId <> ToCurrencyId),
    CONSTRAINT UQ_EXR_Pair_Day UNIQUE (FromCurrencyId, ToCurrencyId, RateDate)
);
CREATE NONCLUSTERED INDEX IX_EXR_Lookup ON ExchangeRates (FromCurrencyId, ToCurrencyId, RateDate) INCLUDE (Rate);

-- 2.2 Revaluation voucher header (NEW, tenant/company-scoped)
CREATE TABLE ExchangeRateRevaluations (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    VoucherNo NVARCHAR(50) NOT NULL,           -- 'ERV-<yyyy>-<seq>', assigned in submit txn
    PostingDate DATE NOT NULL,
    ExchangeGainLossAccountId UNIQUEIDENTIFIER NULL,  -- null = resolve company default at submit
    RoundingLossAllowance DECIMAL(18,4) NOT NULL DEFAULT 0,  -- ERPNext range [0,1)
    DocumentStatus NVARCHAR(20) NOT NULL DEFAULT 'Draft',
    Remarks NVARCHAR(MAX) NULL,
    IdempotencyKey NVARCHAR(100) NULL,
    RowVersion ROWVERSION NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_ERV_Company FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_ERV_FXAccount FOREIGN KEY (ExchangeGainLossAccountId) REFERENCES Accounts(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_ERV_Status CHECK (DocumentStatus IN ('Draft','Submitted','Cancelled')),
    CONSTRAINT CK_ERV_Allowance CHECK (RoundingLossAllowance >= 0 AND RoundingLossAllowance < 1)
);
CREATE UNIQUE NONCLUSTERED INDEX UQ_ERV_Company_VoucherNo ON ExchangeRateRevaluations (CompanyId, VoucherNo);
CREATE UNIQUE NONCLUSTERED INDEX UQ_ERV_Idempotency ON ExchangeRateRevaluations (TenantId, CompanyId, IdempotencyKey)
    WHERE IdempotencyKey IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_ERV_Tenant_Company_Status_Date
    ON ExchangeRateRevaluations (TenantId, CompanyId, DocumentStatus, PostingDate DESC)
    INCLUDE (VoucherNo);

-- 2.3 Derived revaluation-line snapshot (audit of WHAT was restated; mirrors R-13 lines table)
CREATE TABLE ExchangeRateRevaluationLines (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    VoucherId UNIQUEIDENTIFIER NOT NULL,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    BalanceInAccountCurrency DECIMAL(18,4) NOT NULL,  -- Balance_FC at submit
    BalanceInCompanyCurrency DECIMAL(18,4) NOT NULL,  -- carrying Balance_CC at submit
    CurrentExchangeRate DECIMAL(18,6) NOT NULL,
    NewExchangeRate DECIMAL(18,6) NOT NULL,
    NewBalanceInCompanyCurrency DECIMAL(18,4) NOT NULL,
    GainLoss DECIMAL(18,4) NOT NULL,           -- ≠ 0 enforced below (dust rows never persisted)
    ZeroBalance BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_ERVL_Voucher FOREIGN KEY (VoucherId) REFERENCES ExchangeRateRevaluations(Id) ON DELETE CASCADE,
    CONSTRAINT FK_ERVL_Account FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_ERVL_NonZero CHECK (GainLoss <> 0)
);
CREATE NONCLUSTERED INDEX IX_ERVL_Voucher ON ExchangeRateRevaluationLines (VoucherId)
    INCLUDE (AccountId, GainLoss, ZeroBalance);

-- 2.4 Document-currency columns (touch-ups, all NULL/1-compatible with legacy rows)
-- SalesInvoices / PurchaseInvoices: CurrencyId UNIQUEIDENTIFIER NULL, ExchangeRate DECIMAL(18,6) NOT NULL DEFAULT 1
--   + CK (ExchangeRate > 0) each.
-- PaymentEntries: TransactionCurrencyId UNIQUEIDENTIFIER NULL, SettlementExchangeRate DECIMAL(18,6) NULL
--   (null until submit resolves it; then NOT NULL for Submitted/Cancelled — enforced in handler, not CHECK,
--   so Draft rows stay unconstrained).
-- Companies: DefaultExchangeGainLossAccountId UNIQUEIDENTIFIER NULL (FK Accounts NO ACTION)
--   + DefaultExchangeGainLossAccountCode NVARCHAR(50) NULL (code-not-FK, R-13 pattern).

-- 2.5 GLEntry — NO schema change. FX rows are ordinary appends:
--     realized:  VoucherType='PaymentEntry', VoucherNo=<PAY-…>, PostingDate=<payment date>;
--     unrealized: VoucherType='ExchangeRateRevaluation', VoucherNo=<ERV-…>, PostingDate=<revaluation date>.
--     Existing index IX_GLEntry_Tenant_Company_Account_Date covers the revaluation balance query (§3.4).
```

EF mapping notes: `ExchangeRateConfiguration` (global — NO tenant query filter, `Currency` precedent; unique trio; `IsRowVersion`; `date` column type); `ExchangeRateRevaluationConfiguration` (FKs `DeleteBehavior.NoAction`, status `HasConversion<string>`, filtered idempotency unique via `HasFilter`, allowance precision); invoice/payment touch-ups (nullable GUIDs, `decimal(18,6)`); `CompanyConfiguration` touch-up (nullable FX default FK + code). Table names follow the existing plural migration convention; Fase 2 MUST confirm `Companies`/`Accounts`/`Currencies` against `AppDbContextModelSnapshot` before generating the migration (R-13 plan §2 precedent).
Accepted WARNING (dimension 1): no `SYSTEM_VERSIONING` on the new tables — rate corrections are new-row upserts and voucher history lives in the append-only ledger + line snapshots; full temporal history stays deferred with R-32 (R-13 plan §2 precedent).

---

## 3. Posting Rules & Guard Pipelines

### 3.1 Rate resolution (single choke point, FX-01)

```csharp
// IExchangeRateRepository.ResolveRateAsync(fromId, toId, date):
//   1. exact (from → to, date) → Rate
//   2. exact (to → from, date) → Round(1 / Rate, 6)  (guard: never 0 at 6dp, else treat as missing)
//   3. else throw ExchangeRateMissingException  → 422 exchange_rate_missing
// from == to short-circuits to 1 WITHOUT a catalog read (same-currency fast path).
```

### 3.2 Realized FX inside SubmitPaymentEntry (extends R-12, same transaction)

Validation order — every gate runs BEFORE the first row is built (R-12 precedent, zero-row rejection):
1. Existing R-12 gates unchanged (existence, RowVersion, frozen + open-year on `PaymentDate`, `Submit()` state move, bank/counterparty resolution, PE-02/PE-07 allocation revalidation, `payment_currency_mismatch` check per FX-04).
2. Resolve `SettlementRate` = catalog FC → CC at `PaymentDate` (skip when payment currency == functional → rate 1, no FX branch). Persist it on the voucher (`payment.SettlementExchangeRate = rate`).
3. Per allocation slice: `fx = Allocated_FC × (SettlementRate − Invoice.ExchangeRate)`; skip when `|fx| ≤ 0.0001`.
4. Resolve the FX leaf (voucher has none — always the company default chain per FX-03; failure → `invalid_exchange_gain_loss_account`, nothing persisted).
5. Build base R-12 lines with CC restatement (bank/counterparty lines: `Debit/Credit = FC × SettlementRate`, FC columns = FC, `AccountCurrency` = FC code) + FX plug lines per §FX-02 direction rule; `FxCalculator.AssertBalanced(allLines)` (`DoubleEntryGuard`); number, persist voucher + lines atomically.

### 3.3 Realized FX inside CancelPaymentEntry (same transaction)

Reversal = exact mirror of the SUBMITTED set, rebuilt from stored values (`SettlementExchangeRate`, per-slice FX from a deterministic recompute against the stored rate — never a fresh catalog read), same `VoucherNo`, `IsCancelled = true` rows (R-12 `PaymentPosting` precedent: cancel rows carry the flag, FX-06 vs R-13 nuance documented in tasks). Invoice FC outstanding restored per PE-05.

### 3.4 Revaluation submit balance query (single source of truth; preview and submit share it)

```csharp
// Eligible FC position per account, cut off at PostingDate:
from gl in GLEntries
join a in Accounts on gl.AccountId equals a.Id
where gl.TenantId == t && gl.CompanyId == c
   && gl.PostingDate <= postingDate && !gl.IsCancelled
   && (a.RootType == Asset || a.RootType == Liability)
   && !a.IsGroup && a.IsActive && a.CompanyId == c
   && a.CurrencyId != null && a.CurrencyCode != functionalCode
   && !warehouseStockAccountIds.Contains(a.Id)   // inventory stays at cost (Stock W4 linkage)
group by leaf → Balance_FC = Σ(DebitInAccountCurrency − CreditInAccountCurrency),
                Balance_CC = Σ(Debit − Credit); keep when |Balance_FC| > 0.0001 || |Balance_CC| > allowance
per line: CurrentRate = Balance_CC ÷ Balance_FC (|FC| > dust), NewRate = ResolveRateAsync(fc → CC, PostingDate),
          New_CC = Balance_FC × NewRate, Gain = New_CC − Balance_CC
drop |Gain| ≤ 0.0001 rows; dust-leg rule per FX-05; empty survivor set → NoRevaluationGainLossException
```

Fail-closed W4 fallback (dimension 6): if the warehouse→stock-account linkage is unresolvable at Fase 2 (Stock W4 carry-forward still open), every Asset leaf that is NOT positively identified as receivable/bank-role (BankAccount GL link, party/company receivable-payable default) is treated as INELIGIBLE, and the gap is recorded as a WARNING carry-forward for reviewer sign-off. Inventory is never revalued by default.

### 3.5 Hard-lock call (identical two lines as R-13 plan §3)

```csharp
company.EnsurePostingDateUnlocked(postingDate);                                   // AC-04
await _companies.EnsurePostingDateInOpenYearAsync(company.Id, postingDate, token); // FC-04 (R-13 helper, reused)
if (postingDate > today) throw new RevaluationFutureDateException(...);            // FX-07 future guard
```

---

## 4. CQRS Contracts

| Operation | Payload (essential fields) | Returns | Core validation order |
| :--- | :--- | :--- | :--- |
| `UpsertExchangeRate` | `FromCurrencyId, ToCurrencyId, RateDate, Rate + RowVersion?` (no idempotency header — the natural key IS the idempotency: identical payload replays return the row, a changed rate requires `RowVersion`) | `ExchangeRateDto` | currencies exist+active → no self-pair → rate > 0 → create, or RowVersion-guarded correct |
| `GetExchangeRate` | `From, To, RateDate` | `ExchangeRateDto @ EffectiveRate + IsInverse` | §3.1 resolution (absent → `exchange_rate_missing`, HTTP 422 per the single code→status mapping in §5) |
| `CreateExchangeRateRevaluation` | `CompanyId, PostingDate, ExchangeGainLossAccountId?, RoundingLossAllowance?, Remarks? + IdempotencyKey` | `ExchangeRateRevaluationDto(Draft)` | company exists → allowance ∈ [0,1) → frozen + open-year + future-date → account pre-resolution → idempotent replay |
| `SubmitExchangeRateRevaluation` | `VoucherId + RowVersion + IdempotencyKey` | detail + posted line count | Draft? → RowVersion → frozen/open-year/future → FX account re-validated → **§3.4 read** → non-empty? → `FxCalculator` → balance assert → SERIALIZABLE write (lines + GL + `Submitted` + `VoucherNo`) |
| `CancelExchangeRateRevaluation` | `VoucherId + RowVersion + IdempotencyKey` | detail(Cancelled) | Submitted? → FY still open + frozen check → append reversals → status flip |
| `GetRevaluationPreview` | `CompanyId, PostingDate` | `RevaluationPreviewDto(lines + TotalGainLoss)` | same §3.4 read, no writes (frozen/open-year checks included so preview never promises an unpostable set) |
| Payment create/submit/cancel | R-12 contracts + `TransactionCurrencyId?` on create | R-12 DTOs (unchanged shape) | R-12 order + §3.2/§3.3 FX steps |

```csharp
public record ExchangeRateDto(Guid Id, Guid FromCurrencyId, Guid ToCurrencyId, DateOnly RateDate,
    decimal Rate, decimal EffectiveRate, bool IsInverse, byte[] RowVersion);
public record ExchangeRateRevaluationDto(Guid Id, Guid CompanyId, string VoucherNo, DateOnly PostingDate,
    Guid? ExchangeGainLossAccountId, decimal RoundingLossAllowance, string DocumentStatus,
    string? Remarks, byte[] RowVersion);
public record RevaluationLineDto(Guid AccountId, string Code, decimal BalanceFC, decimal BalanceCC,
    decimal CurrentRate, decimal NewRate, decimal NewBalanceCC, decimal GainLoss, bool ZeroBalance);
public record RevaluationPreviewDto(IReadOnlyList<RevaluationLineDto> Lines, decimal TotalGainLoss);
```

---

## 5. API Boundary & RFC 7807 Mapping

Routes: `POST /api/v1/exchange-rates`, `GET /api/v1/exchange-rates?from=&to=&date=`,
`POST /api/v1/exchange-rate-revaluations`, `GET /api/v1/exchange-rate-revaluations?companyId=&status=`,
`GET /api/v1/exchange-rate-revaluations/{id}`, `GET /api/v1/exchange-rate-revaluations/preview?companyId=&postingDate=`,
`POST …/{id}/submit`, `POST …/{id}/cancel` (bodies carry `rowVersion`).
All mutating routes EXCEPT `POST /api/v1/exchange-rates` (natural-key idempotency, §4): `TenantMember` auth + `Idempotency-Key` header REQUIRED (missing → 400 `idempotency_key_required`); `submit`/`cancel`/rate-correct additionally require current `RowVersion`. Payment endpoints keep their R-12 routes (payload-only extension).

| `code` (spec §5) | HTTP | ProblemDetails `title` |
| :--- | :--- | :--- |
| `exchange_rate_invalid`, `exchange_rate_self_pair`, `unknown_currency`, `invalid_exchange_gain_loss_account` | 400 | Bad Request |
| `exchange_rate_missing`, `payment_currency_mismatch`, `no_revaluation_gain_loss`, `revaluation_dust_above_allowance`, `revaluation_future_date` | 422 | Unprocessable Entity |
| `revaluation_not_found` | 404 | Not Found |
| `revaluation_invalid_transition`, `revaluation_already_cancelled`, `fiscal_year_closed`, `fiscal_period_locked`, `concurrency_conflict` | 409 | Conflict |
| `double_entry_imbalance` | 500 | Internal Server Error (internal tripwire) |
| `idempotent_replay` | 200 | OK (recorded response) |

---

## 6. Transaction, Concurrency & Numbering

- Revaluation `Submit`/`Cancel` run in a `SERIALIZABLE` EF execution-strategy transaction: re-read voucher + FY + FX account + §3.4 balances inside the txn; `SaveChanges` once; commit. The FX-R11 race resolves via the fixpoint: loser computes an empty set → `no_revaluation_gain_loss` (no unique index needed, spec FX-06).
- `VoucherNo = $"ERV-{postingDate:yyyy}-{seq}"`, per-company-year `MAX+1` inside the same serializable txn (R-13 §6 precedent; full gapless R-31 deferred).
- `RowVersion` on `ExchangeRate` (correction race) + `ExchangeRateRevaluation` (submit/cancel race); stale token → 409 before any write. Payment FX inherits the R-12 payment `RowVersion` gate (no second token).
- `SettlementExchangeRate` is write-once at submit (handler sets it inside the txn; no command path ever overwrites it afterwards — audit stability per FX-R12).

---

## 7. Test Strategy (mirrored in tasks.md acceptance)

- **Domain unit:** rate guards (negative/self-pair/bad allowance), realized math per direction (gain/loss/zero), unrealized math (gain/loss/dust/empty), transition matrix, FX-account validation matrix, quotation-convention fixture (1 EUR = 1.10 CC).
- **Application integration (SQL Server):** FX-R1/R2/R3 end-to-end (exact GL sets, ΣD−ΣC=0, FC columns populated); FX-R4/R5/R8/R9 rejections persist nothing; FX-R6/R7/R14 revaluation sets; FX-R10 single GL set on replay; FX-R11 parallel-submit single winner; FX-R12/R13 reversals net to zero with originals intact; legacy single-currency regression (all pre-R-14 payment/invoice tests green, byte-identical postings).
- **API:** `code` ↔ status table (§5) per scenario; missing idempotency key → 400; stale rowVersion → 409.
- **UI:** `tsc + vite build + eslint` (no frontend runner per roadmap §2); preview grid matches `preview` endpoint; submit/cancel gated on status + RowVersion.
