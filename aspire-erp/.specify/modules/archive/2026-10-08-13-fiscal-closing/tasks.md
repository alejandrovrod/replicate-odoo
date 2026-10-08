# Implementation Tasks: Fiscal Year + Period Closing Voucher (R-13)

**Module:** `13-fiscal-closing`
**Specification:** [spec.md](./spec.md) · **Technical Plan:** [plan.md](./plan.md)
**Status:** DRAFT — pending review (Fase 1; no code touched)

> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**
**Dependency:** R-12 archived (roadmap ordering rule)

> Each task is atomic and testable. Checkbox `[ ]` = pending Fase 2/3. Invariant tags (FC-xx) map to spec §2; scenarios map to spec §3.

---

## Phase 1: Domain — FiscalYear + hardened PeriodClosingVoucher

- [x] **Task 1.1: `FiscalYear` entity + `EnsurePostingAllowed` guard (FC-04)**
  - Action: create `Erp.Domain/Entities/FiscalYear.cs` (`TenantId, CompanyId, YearName, StartDate, EndDate, IsClosed, ClosedAt, RowVersion, CreatedAt`) with `EnsurePostingAllowed(postingDate)` and `Close()` (close refuses already-closed; re-open method DOES NOT EXIST).
  - Acceptance (unit): `StartDate >= EndDate` rejected; date inside closed year throws `FiscalYearClosedException`; `Close()` on closed year throws; open-year date passes.

- [x] **Task 1.2: `PeriodClosingVoucher` hardening (FC-04/FC-05)**
  - Action: extend entity with `FiscalYearId + FiscalYear nav, Company nav, Account nav, IdempotencyKey`; add `EnsureCanSubmit()/EnsureCanCancel()` transition guards (`Draft→Submitted→Cancelled` only).
  - Acceptance (unit): transition matrix — `Submitted→Submit` throws, `Draft→Cancel` throws, `Cancelled→*` throws; `FiscalYearId == Guid.Empty` rejected.

- [x] **Task 1.3: `FiscalClosingErrorCodes` + exception taxonomy (spec §5)**
  - Action: create Accounting-owned `FiscalClosingErrorCodes` (all snake_case codes from spec §5); add typed exceptions (`FiscalYearClosedException, FiscalYearOverlapException, InvalidRetainedEarningsException, ClosingDateOutsideFiscalYearException, DuplicateClosingForFiscalYearException, NoClosingBalancesException, ClosingNonPLAccountException`); retire `BankingErrorCodes.period_closing_*` usages to the new owner.
  - Acceptance (unit): every code resolves to its §5 HTTP status via the API mapping table; no duplicate string literals in handlers.

- [x] **Task 1.4: Closing-math domain service (FC-01/FC-02/FC-03)**
  - Action: pure `PeriodClosingCalculator.Compute(previewLines) → (offsetLines, retainedLine?, net)` implementing the credit-normal zeroing rule; asserts `ΣD == ΣC ± 0.0001` else `DoubleEntryImbalance`.
  - Acceptance (unit): profit fixture (500k/380k → Cr retained 120k, mirrors AC-05), loss fixture (200k/260k → Dr retained 60k), break-even (no retained row, leaves zeroed), empty input throws `NoClosingBalancesException`, non-P&L input throws `ClosingNonPLAccountException`.

## Phase 2: Infrastructure — DDL, repositories, hard lock retrofit

- [x] **Task 2.1: Migration `AddFiscalYearAndHardenPeriodClosing` (plan §2)**
  - Action: `FiscalYears` table (`CK_FY_Dates`, `UQ_FY_Company_Year`, `IX_FY_Tenant_Company_Closed`), rebuild `PeriodClosingVouchers` (+`FiscalYearId`, FKs `NoAction`, `CK_PCV_Status`, filtered `UQ_One_Submitted_Close_Per_Year`, `UQ_PCV_Company_VoucherNo`, filtered `UQ_PCV_Idempotency`, `IX_PCV_Tenant_Company_Status_Date`), `PeriodClosingVoucherLines` table; EF configurations with `HasFilter`.
  - Acceptance (integration): applying migration to a scratch DB succeeds; inserting overlapping `(CompanyId, YearName)` fails; inserting a second `Submitted` voucher for one year fails at the DB; `StartDate >= EndDate` fails at the DB.

- [x] **Task 2.2: `FiscalYearRepository` + overlap guard (FC-04)**
  - Action: `IFiscalYearRepository` (`GetCoveringYearAsync, HasOverlapAsync, Add/Update, GetPagedAsync`); overlap check runs inside the serializable create transaction.
  - Acceptance (integration): creating `FY-2025` then overlapping `FY-2025b` (2025-06→2026-06) throws `fiscal_year_overlap` and persists nothing.

- [x] **Task 2.3: `PeriodClosingVoucherRepository` rewrite (FC-05)**
  - Action: replace unbounded `GetUnclosedPLEntriesAsync(companyId, postingDate)` with FY-windowed `GetUnclosedPLBalancesAsync(companyId, fiscalYearId)` (plan §4 query, shared by preview + submit); add `HasSubmittedCloseAsync`, `GetVoucherNosOfYearAsync`, `AddReversalEntriesAsync`; DELETE the mutating `UpdateGLEntries` path (reversal-append only).
  - Acceptance (integration): seeded P&L inside/outside the FY window — only inside-window P&L returned; Balance-Sheet leaves never returned; post-close re-query returns empty (fixpoint).

- [x] **Task 2.4: Hard-lock retrofit in ALL posting pipelines (FC-04 hardened AC-04)**
  - Action: insert the two-line plan §3 guard at the top of stock (`CreateStockEntry`, issues), buying (receipt, invoice), selling (delivery, invoice), payment/R-12 submit, journal submit/cancel, and this module's submit/cancel.
  - Acceptance (integration, per pipeline): posting with a date inside a closed FY → `fiscal_year_closed`; date `<= FrozenAccountsDate` → `fiscal_period_locked`; open date passes. Zero regressions on existing pipeline tests.

- [x] **Task 2.5: Seed `3100 - Retained Earnings` + wire company default (FC-03 enabler)**
  - Action: seed script/migration ensuring one active Equity leaf `3100 - Retained Earnings` per company and backfilling `Company.DefaultRetainedEarningsAccountId/Code` where null (spec AC-06 notes the account is not even seeded today — without this, submit can never satisfy FC-03).
  - Acceptance (integration): fresh company has exactly one `3100` Equity leaf and the default resolves; create-voucher with null retained id picks the default and submits (FC-01).

## Phase 3: Application — CQRS handlers

- [x] **Task 3.1: `CreateFiscalYear` + `CloseFiscalYear` + `GetFiscalYears` (FC-04)**
  - Action: commands with validators + idempotency + RowVersion on close; close refuses when a `Draft` voucher exists for the year.
  - Acceptance (integration): create → list → close → `IsClosed=1`; second close / any write to closed year → `fiscal_year_closed`; stale `RowVersion` → `concurrency_conflict`.

- [x] **Task 3.2: `CreatePeriodClosingVoucher` — Draft with pre-checks (FC-03/FC-04)**
  - Action: binds voucher to FY, validates date containment + frozen check + retained resolution (explicit or company default), idempotent replay.
  - Acceptance (integration): date outside FY → `closing_date_outside_fiscal_year`; bad retained (group/inactive/wrong-type/wrong-company) → `invalid_retained_earnings_account`; replay with same key returns same Draft.

- [x] **Task 3.3: `SubmitPeriodClosingVoucher` rewrite (FC-01/02/03/05 — the core)**
  - Action: implement the plan §4 validation order in a SERIALIZABLE txn: guards → duplicate-year check → FY-windowed read → non-empty → retained re-validation → `PeriodClosingCalculator` → balance assert → persist lines + GL + `Submitted` + `VoucherNo`; audit existing naïve handler defects fixed (unbounded window, no FY/frozen/retained/balance/idempotency/RowVersion checks).
  - Acceptance (integration): FC-01 profit / FC-02 loss / FC-03 break-even post exactly the §3 GL sets with `ΣD−ΣC=0`; FC-09 replay yields ONE GL set; FC-10 parallel submits yield ONE winner; FC-12 empty year stays Draft.

- [x] **Task 3.4: `CancelPeriodClosingVoucher` rewrite as reversal (FC-06)**
  - Action: remove `IsCancelled=true` mutation; append mirrored reversal rows + flip status; refuse when FY closed or date frozen.
  - Acceptance (integration): FC-11 — originals byte-identical, reversals net voucher to zero, second cancel → `period_closing_already_cancelled`, cancel on closed FY → `fiscal_year_closed`.

- [x] **Task 3.5: `GetUnclosedPLBalances` preview + detail queries (read-only)**
  - Action: preview returns the exact line set submit would post (+ `Net`); detail returns voucher + lines + GL rows.
  - Acceptance (integration): preview output deep-equals the lines persisted by the subsequent submit for the same FY snapshot.

## Phase 4: API — controllers + RFC 7807

- [x] **Task 4.1: `FiscalYearsController` + `PeriodClosingVouchersController` (§5 routes)**
  - Action: all plan §5 routes; `Idempotency-Key` required on mutating routes; `rowVersion` (base64) required on submit/cancel/close; tenant/company scoping.
  - Acceptance (API integration): route × scenario matrix — each spec §5 code asserts its HTTP status + `code` in the ProblemDetails body; missing key → 400; stale rowVersion → 409.

## Phase 5: UI — FiscalYear master + voucher execution screen

- [x] **Task 5.1: FiscalYear master (`useFiscalYears.ts`, `FiscalYearList.tsx`, `FiscalYearFormModal.tsx`)** — VERIFIED 2026-10-08: tsc+vbuild 2.69s clean, oxlint warnings-only, `FiscalYearList.test.tsx` green
  - Action: list (company scope, `IsClosed` filter, paging) + create form (name/dates + overlap error surfacing) + close button with RowVersion confirm.
  - Acceptance: `tsc + vite build + eslint` clean; closed years render locked and hide the close action.

- [x] **Task 5.2: Closing execution screen (rewrite modal → full flow)** — VERIFIED 2026-10-08: preview+net banner+idempotent Submit+Cancel reversal live in `PeriodClosingList.tsx`; §5 codes localized es/en; `PeriodClosingList.test.tsx` green
  - Action: FY picker (open years only) + retained picker (Equity leaves) + read-only P&L preview grid (from `unclosed-balances`) + net banner + Submit/Cancel buttons gated on status, sending `Idempotency-Key` + `RowVersion`; surface every §5 error code with localized `accounting` namespace strings (en+es).
  - Acceptance: `tsc + vite build + eslint` clean; preview → submit → status flip → cancel reversal visible in ledger; double-click submit posts once (key reuse).

## Phase 6: Verification — full invariant sweep

- [x] **Task 6.1: Adversarial suite + archive** — VERIFIED 2026-10-08: full suite Domain 274/274 + App 524/524 + Integration 98/101 (3 pre-existing order-dependent guard fails, 4/4 on re-run) + frontend 23/23; see `verify-report.md`; archived 2026-10-08
  - Action: run every Phase 1–5 acceptance; prove fixpoint (submit → empty preview), single-close-per-year at DB + app level, append-only ledger (no `UPDATE/DELETE` on `GLEntry` in the close path), cross-pipeline lock coverage; record carry-forwards (re-open, quarterly close, gapless R-31 numbering, FX) and archive per roadmap working agreement.
  - Acceptance: 100% scenarios FC-01…FC-14 green; zero contradictions between spec/plan/tasks; certification badge issued.
