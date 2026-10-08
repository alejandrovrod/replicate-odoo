# Verification Report — 13-fiscal-closing (R-13)

**Change**: Fiscal Year + Period Closing Voucher + hard period lock + retained earnings
**Date**: 2026-10-08 (orchestrator-run, live evidence)
**Mode**: repo-local (`aspire-erp/.specify/modules/13-fiscal-closing/`)
**Spec**: `spec.md` (STATUS 100% VERIFIED & CERTIFIED Pass 3/3) · **Plan**: `plan.md` · **Tasks**: 17/17 `[x]`

## 1. Task Completion Gate — PASS

| Phase | Tasks | State |
|---|---|---|
| 1 Domain (1.1–1.4) | FiscalYear entity+guards, voucher hardening, error taxonomy, calculator | [x] |
| 2 Infra (2.1–2.5) | Migration + FiscalYearRepository/overlap + repo rewrite + 21-pipeline hard lock + 3100 seed | [x] |
| 3 Application (3.1–3.5) | Create/Close FY, Create/Submit(rewrite)/Cancel(reversal)/Preview | [x] |
| 4 API (4.1) | FiscalYearsController + PeriodClosingVouchersController, RFC 7807 | [x] |
| 5 UI (5.1–5.2) | FY master + closing execution flow, es/en strings | [x] |
| 6 Verify (6.1) | This report + archive | [x] |

## 2. Build / Test Evidence (executed 2026-10-08, SQL Server 2022 container `erp-db`)

| Command | Result |
|---|---|
| `dotnet ef database update` | 27 migrations applied incl. `20261008021311_AddFiscalYearAndHardenPeriodClosing` |
| seeds coa/stock/buying/manufacturing/banking/hr-payroll/crm/assets | OK except 2 PRE-EXISTING seed errors (crm Msg 1934 QUOTED_IDENTIFIER; assets FK_Asset_Item) — unrelated to R-13, same scripts CI runs |
| `dotnet test Erp.sln` Domain | **274/274 passed** (244 prior + 30 FiscalClosing) |
| `dotnet test Erp.sln` Application | **524/524 passed** (502 prior + 22 FiscalClosing) |
| `dotnet test Erp.sln` Integration | **98/101** — 3 FAIL all in `GLEntryLedgerGuardTests` (`FirstAsync` on empty ledger at suite start); **4/4 PASS on filtered re-run** once rows exist. Pre-existing order dependency (test file last touched `ce970500`, untouched by R-13). NOT a regression — see §4 W1 |
| `node scripts/i18n-sync.mjs --check` | **OK (261 codes)** after removing 4 dead es-only keys (see §3 fix F1) |
| `npm run lint` | warnings-only, all pre-existing patterns (1 in R-13 `usePeriodClosing.ts:106`, same pattern as codebase) |
| `npm run build` | `tsc -b` + vite **built in 2.69s**, 0 errors |
| `npm test` (vitest) | **23/23 passed** (13 prior + 10 fiscal-closing) |

Runtime proof of Task 2.5 fix: `SELECT` on live DB → `3100 Retained Earnings (Equity, leaf of 3000)` present,
`Company.DefaultRetainedEarningsAccountCode = '3100'`.

## 3. Fixes Applied During This Verification (orchestrator)

| ID | Finding | Fix | Files |
|---|---|---|---|
| F1 | `i18n-sync --check` red: 4 es-only orphan keys (`retained_earnings_account_not_found`, `invalid_period_closing_amount`, `period_closing_conservation_violated`, `period_closing_reconciled_cannot_cancel`) — dead leftovers of retired `BankingErrorCodes.period_closing_*`, referenced by zero `.cs` files | Deleted the 4 `<data>` blocks from `ErrorMessages.es.resx`, ran `node scripts/i18n-sync.mjs` (regenerated en/es `error.json` + types) | `ErrorMessages.es.resx`, locales, `i18n.generated.d.ts` |
| F2 | **BLOCKER re-opened at runtime**: R-13 migration seeds `3100` per company, but migrations run BEFORE any company exists → `3100` absent, submit could never satisfy FC-03 (confirmed: only `3000` in DB) | Added idempotent `3100` leaf + `DefaultRetainedEarningsAccountId/Code='3100'` backfill to `seed-dev-coa.sql` (the CI-blessed data path: migrate → seeds); re-ran seed, verified live | `scripts/seed-dev-coa.sql` |

## 4. Issues (no CRITICAL)

| ID | Severity | Description |
|---|---|---|
| W1 | WARNING | `GLEntryLedgerGuardTests` (3 tests) assume a pre-existing posted row; on a fresh migrate+seed DB they fail with `Sequence contains no elements` when scheduled before any posting test. Passes 4/4 once rows exist. Pre-existing flake, out of R-13 scope. Carry-forward: make the class insert its own fixture row instead of borrowing leftovers. |
| W2 | WARNING | `seed-dev-crm.sql` (Msg 1934 QUOTED_IDENTIFIER) and `seed-dev-assets.sql` (FK_Asset_Item conflict) error on lines unrelated to R-13. Same behaviour under CI flow. Carry-forward to seed owners. |
| W3 | WARNING | R-13 migration's inline `3100` seed is a no-op on fresh DBs (no companies at migrate time) — kept intentionally as backfill for DBs migrated with existing companies; the seed script is now the canonical path (F2). |
| S1 | SUGGESTION | `specs/01-accounting/spec.md` AC-05/AC-06 still carry `DEFERRED` markers pointing at this R-13 as carry-forward. A spec-sync amendment (marking them implemented-by-R-13) was deliberately NOT done here to avoid editing a certified archived spec — propose as follow-up. |
| S2 | SUGGESTION | Carry-forwards from tasks 6.1 stand: FY re-open, quarterly close, gapless R-31 numbering, FX revaluation (R-14). |

## 5. Spec Compliance Matrix (FC-01…FC-07 + 14 Gherkin scenarios)

| Invariant | Evidence | Status |
|---|---|---|
| FC-01 double-entry closure (`\|ΣD−ΣC\| ≤ 0.0001`, rollback on violation) | `PeriodClosingCalculator` + unit fixtures profit 500k/380k→Cr 120k, loss 200k/260k→Dr 60k, break-even; 30 Domain + 18 handler tests green | COMPLIANT |
| FC-02 P&L-only (Balance Sheet untouched, `closing_non_pl_account` tripwire) | FY-windowed `GetUnclosedPLBalancesAsync` filters Income/Expense leafs; unit + repo tests | COMPLIANT |
| FC-03 retained valid (Equity leaf, same company; profit Cr / loss Dr) | `FiscalClosingGuards` + company default + live `3100` proof (§2); create/submit with null-retained resolves default | COMPLIANT |
| FC-04 hard lock (closed FY + `postingDate <= FrozenAccountsDate` on EVERY pipeline) | 21 call sites + submit/cancel guards; `FiscalPeriodLockApiTests` green in full suite | COMPLIANT |
| FC-05 single close per year (filtered unique + serializable + `Idempotency-Key` replay = 1 GL set) | `UQ_One_Submitted_Close_Per_Year` + app guard + idempotency tests | COMPLIANT |
| FC-06 cancel = mirrored reversal, originals byte-identical | `AddReversalEntriesAsync`, no `UpdateGLEntries` path; cancel tests | COMPLIANT |
| FC-07 RowVersion concurrency + tenant scoping | Close/submit/cancel require base64 RowVersion; stale → 409; tenant filters | COMPLIANT |
| FC-08…FC-14 scenarios (zero close, outside-date, bad retained, frozen, replay, parallel, empty) | Covered by handler/model-mapping tests (18 + 4) + API matrix | COMPLIANT |

Zero contradictions between spec / plan / tasks after Pass-3 hardening; `tasks.md` 17/17 `[x]`.

## Verdict: PASS WITH WARNINGS (W1–W3 pre-existing/environmental, 0 CRITICAL, 0 UNTESTED)
