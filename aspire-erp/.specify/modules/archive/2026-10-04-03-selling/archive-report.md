# Archive Report — 03-selling

**Change**: 03-selling
**Date**: 2026-10-04
**Mode**: openspec (artifact store) / repo-local (planning home)
**Change root (before archive)**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\03-selling`
**Archived to**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\archive\2026-10-04-03-selling\`
**Outcome**: Archived as `intentional-with-warnings`

## Gate Results

| Gate | Result | Evidence |
|------|--------|----------|
| Task Completion Gate | PASS | `tasks.md`: 6/6 boxes checked `[x]` (5.1, 5.2, 5.2b, 5.3, 5.4, 5.5), 0 unchecked — with 5.3/5.4/5.5 annotated DEFERRED (W11 fix, this commit) |
| Verify verdict | PASS WITH WARNINGS | Re-verification at HEAD `1a17c866`: build 0 warnings / 0 errors (verifier-run, 3rd attempt — first two hit concurrent-build file locks, not code); orchestrator full suite 370/370 at this exact code state; verifier narrow re-runs 49/49 (41 Application + 8 Domain) |
| CRITICAL issues | 0 — none (C1–C5 retired to OUT OF SCOPE by amendment, see below) | Report states "CRITICAL: None"; 0 UNTESTED, 0 FAILING in-scope rows |
| Archive override | `intentional-with-warnings` | Standing user order for the 01/02/03 retro-archive, with the 04-buying, 02-stock and 01-accounting precedents; 6 open warnings acknowledged below |
| Action context guard | PASS | `mode: repo-local`, all operations inside `allowedEditRoots: C:\Workspace\Odoo\aspire-erp` |

## C1–C5 Remediation Record (FAIL → PASS WITH WARNINGS)

The first verification of this module returned **Verdict FAIL** with five CRITICALs: the sales-invoice posting path is DI-registered but routed to no endpoint (`TaxTotal` forced to 0, zero tests), the POS checkout cannot run end to end (unseeded hard-coded customer/profile GUIDs in `PosCashierModal.tsx`, tax-inclusive tender vs tax-zero server, no COGS/stock pair, unguarded endpoint, zero tests), no sales-invoice mutation carries `[IdempotencyKeyRequired]`, no `CancelSalesInvoice` handler/endpoint/test exists, and the SL-06 concurrency scenario is untested with `RowVersion` conflicts mapping to `server_error` — all under a "100% PRODUCTION CERTIFIED" header. The orchestrator confirmed the pattern (same false-certification class as 01/02) and the second verifier re-checked every marker claim line-by-line against the source ("Amendment integrity check": accurate, nothing papered over).

Per the user's explicit decision (amend-spec-to-reality over implement-the-gaps or archive-with-override), commit `1a17c866` reconciled the contract instead of building the missing sales-invoice/POS/cancellation scope:

1. Invariant SL-01, invariant SL-03 and scenarios SL-03, SL-04, SL-05, SL-06 now carry inline `DEFERRED — scope amendment 2026-10-04 (retro-verify)` markers; the SL-02 credit-invoice clause and the SL-01 invoice half are explicitly scoped out (SalesOrder path and DeliveryNote half stay certified). History preserved, nothing deleted.
2. The spec header now reads "CERTIFIED — scope amended …" instead of the false "100% PRODUCTION CERTIFIED".
3. A second, independent re-verification at HEAD `1a17c866` returned **PASS WITH WARNINGS, 0 CRITICAL** — 3/4 in-scope rows COMPLIANT, 1 PARTIAL (SL-02 message literal), 6 rows OUT OF SCOPE. The full re-verification is this folder's `verify-report.md`.

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| 03-selling | Created (copy — main spec did not exist) | `.specify\modules\03-selling\spec.md` → `.specify\specs\03-selling\spec.md`; module spec is both delta and full spec (v2.0.0 + 2026-10-04 scope amendment, 4 invariants with SL-01/SL-03 DEFERRED, 6 scenarios with SL-03..SL-06 DEFERRED); no merge performed, no existing main spec overwritten |

## File Operations Executed (orchestrator, in order)

1. Create `.specify\specs\03-selling\`
2. Copy `.specify\modules\03-selling\spec.md` → `.specify\specs\03-selling\spec.md` (spec sync BEFORE move)
3. Write `verify-report.md` and `archive-report.md` inside the change root
4. Annotate `tasks.md` 5.3/5.4/5.5 as DEFERRED + flip the status line (W11 fix, in the same commit)
5. Move `.specify\modules\03-selling\` → `.specify\modules\archive\2026-10-04-03-selling\` (contains spec.md, plan.md, tasks.md, verify-report.md, archive-report.md)
6. Repair the 03-selling row in the root index tables (`.specify\plan.md`, `.specify\spec.md`, `.specify\tasks.md`): links `./modules/03-selling/…` → `./modules/archive/2026-10-04-03-selling/…` AND certification `100% CERTIFIED` → `CERTIFIED — amended scope`

Post-conditions confirmed after execution: main spec exists and is byte-identical (SHA-256 equal) to the archived `spec.md`; `modules\03-selling` no longer exists; archive folder contains exactly the 5 artifacts; archived `tasks.md` shows 6/6 `[x]` with deferral annotations on 5.3–5.5.

## Archive Contents Checklist

- [x] spec.md — functional specification v2.0.0 + scope amendment (source of truth, also synced to `.specify\specs\03-selling\spec.md`)
- [x] plan.md — technical design (unchanged; Amendment A2 drift recorded as W6)
- [x] tasks.md — 6/6 boxes checked, 5.3–5.5 annotated DEFERRED
- [x] verify-report.md — PASS WITH WARNINGS (re-verification at `1a17c866`)
- [x] archive-report.md — this file
- [ ] proposal.md — MISSING (by design; see below)

## Missing Artifacts Record

- **proposal.md**: absent. This module's workflow starts at spec/plan (no separate proposal phase artifact was produced); same condition recorded for 04-buying, 02-stock and 01-accounting. Reported per skill rule "Missing proposal/spec/design artifacts should be reported."
- **applyProgress file**: absent as a standalone artifact; this repo tracks apply progress as task checkboxes inside `tasks.md`.

## Deferred Scope — Carry-Forward Flags (explicit backlog for re-certification)

- **Sales-invoice posting (SL-01 + SL-02 invoice clause + SL-01 invoice half)**: route `CreateSalesInvoice`/`SubmitSalesInvoice` **or delete them** (DI-registered but unreachable handlers invite false assumptions); `TaxTotal` forced 0; `BilledPercentage`/`BilledQuantity` never written (W5); `Customer.OutstandingAmount` never updated by reachable code (ex-W4).
- **POS checkout (SL-03 + SL-04 + SL-03 invariant)**: seed a real POS profile/customer (or add a `POSProfile` API), tender the server-quoted total, add COGS/stock pair + FIFO guard + `[IdempotencyKeyRequired]`, cover with a POS integration test asserting the SL-03 GL quartet (ex-W2/W3).
- **Cancellation/credit note (SL-05)**: mirror `CancelPurchaseInvoiceCommandHandler` (status gate, swapped reversal, optional SLE return) — the last entirely missing scenario.
- **Concurrency (SL-06)**: `RowVersion` conflict currently maps to `server_error` instead of `CreditLimitExceededException`; add the credit-race test when the invoice scope is un-deferred.
- Re-certify the module when this backlog lands.

## Warnings Acknowledgment (verify report, non-blocking)

Verdict **PASS WITH WARNINGS**, 0 CRITICAL. Archive accepted with these acknowledged warnings:

- **W1 (SAFETY FLAG, still WARNING)** — `SalesPostingService.PostDeliveryNoteAsync` reads FIFO layers WITHOUT the stock range lock (only `StockPostingService` takes it); concurrent delivery notes on one item/warehouse can oversell. Ranked WARNING because no in-scope scenario exercises the race; disclosed in spec SL-06 marker. Remediation is a one-line `LockStockRangeAsync` call + a two-orders-one-item concurrency test — cheapest insurance for archived ST-03. Escalates to CRITICAL the moment any scenario asserts non-overselling under concurrency.
- **W5** — `BilledPercentage`/`BilledQuantity` dead columns (billing deferred; plan/task docs not updated).
- **W6** — `plan.md` Amendment A2 not amended (spec-only amendment): `SalesInvoiceLine` tax columns and company tax-code columns don't exist.
- **W7** — In-scope SL-02 literal drift (message asserted by shape, not by literal; no test pins the string).
- **W10** — Unproven delivery-note replay (guard present, only missing-key 400 tested).
- **W11** — Task/status drift fixed in this commit (module annotations + root index row).

Former W2/W3/W4/W8/W9 reclassified DEFERRED (kept as the carry-forward flags above). Suggestions (6) carry forward as non-blocking follow-ups.

## Layout-Mapping Note

This repo does NOT use `openspec/changes/…`; the change space is `.specify\modules\` (user-approved fork, decided in the 04-buying archive session):

- OpenSpec `openspec/specs/{domain}/spec.md` maps to `.specify\specs\03-selling\spec.md`.
- OpenSpec `openspec/changes/{change-name}/` maps to `.specify\modules\03-selling\` (pre-archive).
- OpenSpec `openspec/changes/archive/YYYY-MM-DD-{change-name}/` maps to `.specify\modules\archive\2026-10-04-03-selling\`.
- `openspec/config.yaml` does not exist → `rules.archive` not applicable.

## Traceability

- Repository HEAD at re-verification: `1a17c866` (verify run made no edits/commits; `git status --porcelain` empty).
- Scope-amendment commit: `1a17c866` (`docs(sdd): amend 03-selling spec to mark deferred scope`).
- Observation IDs: not applicable (artifact store = `openspec`; artifacts are filesystem files, not Engram observations). Related Engram observations: `#187` (amend-vs-implement decision record).
