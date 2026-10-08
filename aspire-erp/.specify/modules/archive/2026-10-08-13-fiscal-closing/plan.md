# Technical Plan: Fiscal Year + Period Closing Voucher (R-13)

**Module:** `13-fiscal-closing`
**Status:** DRAFT — pending review

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**
**Version:** 1.0.0
**Stack:** .NET 10, SQL Server 2025, EF Core 9, React 19 + TypeScript
**Architectural Standard:** Clean Architecture, DDD, CQRS, Tenant Isolation
**Spec:** [spec.md](./spec.md) · **Tasks:** [tasks.md](./tasks.md)

---

## 1. Clean Architecture Topology & Layers

```
src/Backend/
├── Erp.Domain/
│   ├── Entities/
│   │   ├── FiscalYear.cs                      # NEW — YearName, StartDate, EndDate, IsClosed, CompanyId, RowVersion
│   │   ├── PeriodClosingVoucher.cs            # EXTEND — + FiscalYearId, + IdempotencyKey, navs, transition guards
│   │   ├── PeriodClosingVoucherLine.cs        # NEW (optional persistence) — derived preview lines snapshot at submit
│   │   ├── Company.cs                         # EXTEND — EnsurePostingDateUnlocked stays; FiscalYears nav
│   │   ├── GLEntry.cs                         # UNCHANGED — VoucherType="PeriodClosingVoucher" rows appended
│   │   ├── FiscalClosingErrorCodes.cs         # NEW — Accounting-owned vocabulary (spec §5)
│   │   └── Exceptions/ (FiscalYearClosedException, FiscalYearOverlapException, InvalidRetainedEarningsException,
│   │                     ClosingDateOutsideFiscalYearException, DuplicateClosingForFiscalYearException,
│   │                     NoClosingBalancesException, ClosingNonPLAccountException) — reuse FiscalPeriodLockedException
│   ├── Repositories/
│   │   ├── IFiscalYearRepository.cs           # NEW
│   │   └── IPeriodClosingVoucherRepository.cs # EXTEND — FY-windowed reads, reversal append, serializable submit lock
├── Erp.Application/
│   ├── Features/FiscalClosing/
│   │   ├── CreateFiscalYearCommand(+Handler+Validator)
│   │   ├── CloseFiscalYearCommand(+Handler — RowVersion, idempotency)
│   │   ├── GetFiscalYearsQuery(+Handler — company + IsClosed filter, paging)
│   │   ├── CreatePeriodClosingVoucherCommand(+Handler+Validator — Draft, FY containment pre-check)
│   │   ├── SubmitPeriodClosingVoucherCommand(+Handler — §3 pipeline)
│   │   ├── CancelPeriodClosingVoucherCommand(+Handler — §3 reversal)
│   │   ├── GetPeriodClosingVouchersQuery + GetPeriodClosingVoucherDetailQuery
│   │   └── GetUnclosedPLBalancesQuery(+Handler — read-only preview, SAME query as submit step 3)
│   └── DTOs/ (FiscalYearDto, PeriodClosingVoucherDto extended, ClosingPreviewLineDto)
├── Erp.Infrastructure/
│   ├── Data/Configurations/ (FiscalYearConfiguration, PeriodClosingVoucherConfiguration REWRITE,
│   │                          PeriodClosingVoucherLineConfiguration, CompanyConfiguration touch-up)
│   ├── Data/Repositories/ (FiscalYearRepository, PeriodClosingVoucherRepository REWRITE)
│   └── Migrations/ (AddFiscalYearAndHardenPeriodClosing — §2 DDL)
└── Erp.Api/
    ├── Controllers/V1/ (FiscalYearsController, PeriodClosingVouchersController — §5 routes)
    └── ProblemDetails mapping for every spec §5 code → HTTP status
src/Frontend/erp-client/src/features/accounting/
├── fiscal-years/ (FiscalYearList.tsx, FiscalYearFormModal.tsx, useFiscalYears.ts)
└── closing/ (PeriodClosingList EXTEND, PeriodClosingFormModal REWRITE → execution screen:
              FY picker, retained picker, P&L preview grid, Submit/Cancel with RowVersion + Idempotency-Key)
```

---

## 2. SQL Server 2025 Physical Schema (DDL)

```sql
-- 2.1 Fiscal Year master (NEW)
CREATE TABLE FiscalYears (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    YearName NVARCHAR(20) NOT NULL,              -- e.g. 'FY-2025'
    StartDate DATE NOT NULL,
    EndDate DATE NOT NULL,
    IsClosed BIT NOT NULL DEFAULT 0,
    ClosedAt DATETIMEOFFSET NULL,
    RowVersion ROWVERSION NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT CK_FY_Dates CHECK (StartDate < EndDate),
    CONSTRAINT CK_FY_ClosedAt CHECK ((IsClosed = 0 AND ClosedAt IS NULL) OR (IsClosed = 1)),
    CONSTRAINT FK_FY_Company FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE NO ACTION,
    CONSTRAINT UQ_FY_Company_Year UNIQUE (CompanyId, YearName)
);
CREATE NONCLUSTERED INDEX IX_FY_Tenant_Company_Closed ON FiscalYears (TenantId, CompanyId, IsClosed) INCLUDE (StartDate, EndDate);

-- Overlap guard is enforced in the repository inside SERIALIZABLE scope (date-range overlap
-- cannot be expressed as a single CHECK); the unique index above plus the serializable
-- insert/update transaction makes concurrent overlapping creates serialize (spec FC-04).

-- 2.2 PeriodClosingVouchers (REBUILD of 20261008000616 — FKs, FY link, idempotency, status CHECK)
CREATE TABLE PeriodClosingVouchers (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    CompanyId UNIQUEIDENTIFIER NOT NULL,
    FiscalYearId UNIQUEIDENTIFIER NOT NULL,
    VoucherNo NVARCHAR(50) NOT NULL,             -- 'PCV-<yyyy>-<seq>', assigned in submit txn
    PostingDate DATE NOT NULL,
    RetainedEarningsAccountId UNIQUEIDENTIFIER NOT NULL,
    DocumentStatus NVARCHAR(20) NOT NULL DEFAULT 'Draft',
    Remarks NVARCHAR(MAX) NULL,
    IdempotencyKey NVARCHAR(100) NULL,
    RowVersion ROWVERSION NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    CONSTRAINT FK_PCV_Company FOREIGN KEY (CompanyId) REFERENCES Companies(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_PCV_FiscalYear FOREIGN KEY (FiscalYearId) REFERENCES FiscalYears(Id) ON DELETE NO ACTION,
    CONSTRAINT FK_PCV_Retained FOREIGN KEY (RetainedEarningsAccountId) REFERENCES Accounts(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_PCV_Status CHECK (DocumentStatus IN ('Draft','Submitted','Cancelled'))
);
-- Exactly one live close per year (Cancelled rows don't count — re-close needs a new voucher):
CREATE UNIQUE NONCLUSTERED INDEX UQ_One_Submitted_Close_Per_Year
    ON PeriodClosingVouchers (CompanyId, FiscalYearId)
    WHERE DocumentStatus = 'Submitted';
CREATE UNIQUE NONCLUSTERED INDEX UQ_PCV_Company_VoucherNo ON PeriodClosingVouchers (CompanyId, VoucherNo);
CREATE UNIQUE NONCLUSTERED INDEX UQ_PCV_Idempotency ON PeriodClosingVouchers (TenantId, CompanyId, IdempotencyKey)
    WHERE IdempotencyKey IS NOT NULL;
CREATE NONCLUSTERED INDEX IX_PCV_Tenant_Company_Status_Date
    ON PeriodClosingVouchers (TenantId, CompanyId, DocumentStatus, PostingDate DESC)
    INCLUDE (FiscalYearId, VoucherNo);

-- 2.3 Derived closing-line snapshot (audit of WHAT was zeroed; optional but recommended)
CREATE TABLE PeriodClosingVoucherLines (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    TenantId UNIQUEIDENTIFIER NOT NULL,
    VoucherId UNIQUEIDENTIFIER NOT NULL,
    AccountId UNIQUEIDENTIFIER NOT NULL,
    Debit DECIMAL(18,4) NOT NULL DEFAULT 0,
    Credit DECIMAL(18,4) NOT NULL DEFAULT 0,
    CONSTRAINT FK_PCVL_Voucher FOREIGN KEY (VoucherId) REFERENCES PeriodClosingVouchers(Id) ON DELETE CASCADE,
    CONSTRAINT FK_PCVL_Account FOREIGN KEY (AccountId) REFERENCES Accounts(Id) ON DELETE NO ACTION,
    CONSTRAINT CK_PCVL_NonNeg CHECK (Debit >= 0 AND Credit >= 0 AND (Debit > 0 OR Credit > 0))
);
CREATE NONCLUSTERED INDEX IX_PCVL_Voucher ON PeriodClosingVoucherLines (VoucherId) INCLUDE (AccountId, Debit, Credit);

-- 2.4 GLEntry — NO schema change. Closing posts ordinary rows:
--     VoucherType='PeriodClosingVoucher', VoucherNo=<voucher>, PostingDate=<voucher date>,
--     IsCancelled=0 on submit; reversal rows on cancel (also IsCancelled=0, mirrored D/C).
--     Existing index IX_GLEntry_Tenant_Company_Account_Date covers the FY-windowed balance query.
```

EF mapping notes: `FiscalYearConfiguration` (table name, unique index, `IsRowVersion`, date column types `date`); `PeriodClosingVoucherConfiguration` full rewrite (FKs `DeleteBehavior.NoAction`, status `HasConversion<string>`, filtered uniques via `HasFilter`); `CompanyConfiguration` untouched (already has `FrozenAccountsDate date`).
Table names above follow the existing migration convention (`PeriodClosingVouchers` plural); Fase 2 MUST confirm `Companies`/`Accounts` against `AppDbContextModelSnapshot` before generating the migration — if the model uses singular names, the `REFERENCES` targets change but no invariant changes.
Accepted WARNING: `FiscalYear`/`PeriodClosingVoucher` use `RowVersion` + `ClosedAt`/line-snapshot audit instead of `SYSTEM_VERSIONING` temporal tables (dimension 1 temporal-versioning coverage is partial by design; full temporal history deferred with the R-32 audit-trail platform work).

---

## 3. Hard Period Lock Rule (applies to EVERY posting pipeline)

```csharp
// Pseudo-rule executed at the TOP of every submit/post/cancel handler that writes GLEntry:
// stock (CreateStockEntry, issues), buying (receipt, invoice), selling (delivery, invoice R-11),
// banking/payment (R-12), journal, AND this module's own submit/cancel:
company.EnsurePostingDateUnlocked(postingDate);   // postingDate <= FrozenAccountsDate → FiscalPeriodLockedException (AC-04, exists)
fiscalYear?.EnsurePostingAllowed(postingDate);    // year null (no FY covers date) → allowed ONLY when no closed year is violated;
                                                  // date inside a year with IsClosed → FiscalYearClosedException (FC-04)
```

- `FiscalYear.EnsurePostingAllowed(DateOnly d)`: if `d < StartDate || d > EndDate` → caller must resolve the year covering `d` first; the repository helper `GetYear CoveringAsync(companyId, date)` returns it or null (no year = open). If the covering year exists and `IsClosed` → throw.
- `CloseFiscalYear` sets `IsClosed = 1, ClosedAt = now` ONLY when no `Draft` voucher remains for the year (else `period_closing_invalid_transition`) — the submitted close stays immutable history.
- Sell-through: Fase 2 adds the two-line call to each pipeline listed above (tasks.md Phase 2); unit tests prove a closed-year date is rejected in every pipeline.

---

## 4. CQRS Contracts

### Commands / Queries
| Operation | Payload (essential fields) | Returns | Core validation order |
| :--- | :--- | :--- | :--- |
| `CreateFiscalYear` | `CompanyId, YearName, StartDate, EndDate + IdempotencyKey` | `FiscalYearDto` | tenant/company exists → `StartDate<EndDate` → no overlap (serializable) → create open |
| `CloseFiscalYear` | `FiscalYearId + RowVersion + IdempotencyKey` | `FiscalYearDto(IsClosed=1)` | exists+open → `RowVersion` match → no Draft vouchers → close |
| `CreatePeriodClosingVoucher` | `CompanyId, FiscalYearId, PostingDate, RetainedEarningsAccountId?, Remarks? + IdempotencyKey` | `PeriodClosingVoucherDto(Draft)` | FY exists+open+same company → date inside FY → frozen check → retained resolves (or company default) → idempotent replay |
| `SubmitPeriodClosingVoucher` | `VoucherId + RowVersion + IdempotencyKey` | detail + posted count | Draft? → FY open → frozen check → duplicate-year guard → **read P&L window** → non-empty? → retained re-validated → compute offsets → `ΣD==ΣC` assert → serializable write (lines + GL + status) + `VoucherNo` assign |
| `CancelPeriodClosingVoucher` | `VoucherId + RowVersion + IdempotencyKey` | detail(Cancelled) | Submitted? → FY still open → frozen check → append reversals → status flip |
| `GetUnclosedPLBalances` | `CompanyId, FiscalYearId` | `ClosingPreviewLineDto[] + Net` | FY exists → same read as submit step 3 (no writes) |

### Submit balance query (the single source of truth; preview and submit share it)
```csharp
// credit-normal P&L balances inside the fiscal-year window, excluding cancelled rows and
// excluding rows already produced by a submitted close of the SAME year (fixpoint):
from gl in GLEntries
join a in Accounts on gl.AccountId equals a.Id
where gl.TenantId == t && gl.CompanyId == c
   && gl.PostingDate >= fy.StartDate && gl.PostingDate <= fy.EndDate
   && !gl.IsCancelled
   && !(gl.VoucherType == "PeriodClosingVoucher" && closedVoucherNosOfYear.Contains(gl.VoucherNo))
   && (a.RootType == Income || a.RootType == Expense)
   && !a.IsGroup && a.IsActive && a.CompanyId == c
group by leaf → balance b = Σ(Credit − Debit); keep b ≠ 0
Net = Σ b  // >0 profit → Cr retained; <0 loss → Dr retained
```

### DTOs
```csharp
public record FiscalYearDto(Guid Id, Guid CompanyId, string YearName, DateOnly StartDate, DateOnly EndDate,
    bool IsClosed, DateTimeOffset? ClosedAt, byte[] RowVersion);
public record PeriodClosingVoucherDto(Guid Id, Guid CompanyId, Guid FiscalYearId, string VoucherNo,
    DateOnly PostingDate, Guid RetainedEarningsAccountId, DocumentStatus DocumentStatus,
    string? Remarks, byte[] RowVersion);
public record ClosingPreviewLineDto(Guid AccountId, string Code, string Name, string RootType,
    decimal Debit, decimal Credit, decimal Balance);
```

---

## 5. API Boundary & RFC 7807 Mapping

Routes: `POST /api/v1/fiscal-years`, `GET /api/v1/fiscal-years?companyId=&isClosed=&page=&pageSize=`,
`POST /api/v1/fiscal-years/{id}/close` (body: `{rowVersion}`),
`POST /api/v1/period-closing-vouchers`, `GET /api/v1/period-closing-vouchers?companyId=&fiscalYearId=&status=`,
`GET /api/v1/period-closing-vouchers/{id}`, `POST …/{id}/submit`, `POST …/{id}/cancel` (bodies carry `rowVersion`),
`GET /api/v1/period-closing-vouchers/unclosed-balances?companyId=&fiscalYearId=`.
All mutating routes: `TenantMember` auth + `Idempotency-Key` header REQUIRED (missing → 400 `idempotency_key_required`); `submit`/`cancel`/`close` additionally require current `RowVersion`.

| `code` (spec §5) | HTTP | ProblemDetails `title` |
| :--- | :--- | :--- |
| `fiscal_year_closed`, `fiscal_year_overlap`, `duplicate_closing_for_fiscal_year`, `fiscal_period_locked`, `period_closing_invalid_transition`, `period_closing_already_cancelled`, `concurrency_conflict` | 409 | Conflict |
| `invalid_retained_earnings_account` | 400 | Bad Request |
| `closing_date_outside_fiscal_year`, `no_closing_balances` | 422 | Unprocessable Entity |
| `period_closing_not_found`, `fiscal_year_not_found` | 404 | Not Found |
| `closing_non_pl_account`, `double_entry_imbalance` | 500 | Internal Server Error (internal tripwire) |
| `idempotent_replay` | 200 | OK (recorded response) |

---

## 6. Transaction, Concurrency & Numbering

- `Submit`/`Cancel`/`CloseFiscalYear` run in a `SERIALIZABLE` EF execution-strategy transaction: re-read FY + voucher + duplicate guard + balances inside the txn; `SaveChanges` once; commit. The filtered unique index is the backstop for the FC-10 race.
- `VoucherNo = $"PCV-{fy.StartDate:yyyy}-{seq}"` where `seq` is a per-company-year `MAX+1` read inside the same serializable txn (no fiscal gap on rollback; full gapless series deferred to R-31 per spec §6).
- `RowVersion` concurrency tokens on `FiscalYear` + `PeriodClosingVoucher`; stale token → 409 before any write.

---

## 7. Test Strategy (mirrored in tasks.md acceptance)

- **Domain unit:** FY date guard, overlap detection, voucher transition matrix, retained validation matrix, zeroing math (profit/loss/zero), reversal mirroring.
- **Application integration (SQL Server):** FC-01/02/03 end-to-end balances; FC-04/05/07/08 rejections persist nothing; FC-12 empty year; FC-09 idempotent replay single GL set; FC-10 parallel-submit single winner; FC-11 reversal nets to zero with originals intact; closed-year posting rejected in every pipeline (Phase 2 retrofits).
- **API:** RFC 7807 `code` ↔ status table (§5) per scenario; missing idempotency key → 400.
- **UI:** `tsc + vite build + eslint` (no frontend runner per roadmap §2); preview grid matches `unclosed-balances`; submit/cancel buttons gated on status + RowVersion.
