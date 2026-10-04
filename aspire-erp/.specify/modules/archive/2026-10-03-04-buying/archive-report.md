# Archive Report — 04-buying

**Change**: 04-buying
**Date**: 2026-10-03
**Mode**: openspec (artifact store) / repo-local (planning home)
**Change root (before archive)**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\04-buying`
**Archived to**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\archive\2026-10-03-04-buying\`
**Outcome**: Archived as `intentional-with-warnings`

## Gate Results

| Gate | Result | Evidence |
|------|--------|----------|
| Task Completion Gate | PASS | `tasks.md`: 7/7 implementation tasks checked `[x]` (4.1–4.7), 0 unchecked |
| Verify verdict | PASS WITH WARNINGS | Build 0 warnings / 0 errors; 347/347 tests passed (86 unit + 216 application + 45 integration); exit code 0 |
| CRITICAL issues | 0 — none | Report states "CRITICAL: None"; zero FAILING, zero UNTESTED scenarios |
| Archive override | `intentional-with-warnings` | User explicitly ordered archive after reviewing the verify report (10 non-critical warnings, W1–W10) |
| Action context guard | PASS | `mode: repo-local`, all operations inside `allowedEditRoots: C:\Workspace\Odoo\aspire-erp` |

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| 04-buying | Created (copy — main spec did not exist) | `.specify\modules\04-buying\spec.md` → `.specify\specs\04-buying\spec.md`; module spec is both delta and full spec (v2.0.0, 3 invariants BY-01..03, 6 scenarios BY-01..06); no merge performed, no existing main spec overwritten |

## File Operations Executed (orchestrator, in order)

1. Create `.specify\specs\04-buying\`
2. Copy `.specify\modules\04-buying\spec.md` → `.specify\specs\04-buying\spec.md` (spec sync BEFORE move)
3. Create `.specify\modules\archive\`
4. Move `.specify\modules\04-buying\` → `.specify\modules\archive\2026-10-03-04-buying\` (contains spec.md, plan.md, tasks.md, verify-report.md)

Post-conditions confirmed after execution: main spec exists and is byte-identical (SHA-256 equal) to the archived `spec.md`; `modules\04-buying` no longer exists; archive folder contains exactly the 4 artifacts; archived `tasks.md` still shows 7/7 `[x]`.

## Archive Contents Checklist

- [x] spec.md — functional specification v2.0.0 (source of truth, also synced to `.specify\specs\04-buying\spec.md`)
- [x] plan.md — technical design
- [x] tasks.md — 7/7 implementation tasks complete
- [x] verify-report.md — PASS WITH WARNINGS
- [ ] proposal.md — MISSING (by design; see below)

## Missing Artifacts Record

- **proposal.md**: absent. This module's workflow starts at spec/plan (no separate proposal phase artifact was produced). The user explicitly ordered this archive after reviewing the verify report. Recorded per skill rule "Missing proposal/spec/design artifacts should be reported."
- **applyProgress file**: absent as a standalone artifact; this repo tracks apply progress as task checkboxes inside `tasks.md` (7/7 `[x]`).

## Warnings Acknowledgment (verify report, non-blocking)

Verdict **PASS WITH WARNINGS**, 0 CRITICAL. User-ordered archive accepted with these acknowledged warnings:

- **W1** — Spec BY-05 stock-reversal clause not implemented or tested (scenario PARTIAL).
- **W2** — Spec BY-02 account literal `1350` does not exist; chart uses `1130`.
- **W3** — `plan.md` §2 validator sample text stale vs spec literals (spec wins).
- **W4** — Tax withholding has no implementation path (`WithholdingTaxTotal` hard-coded 0).
- **W5** — Task 4.5 acceptance unmet: PO progress fields never assigned → "Unbilled Receipts" card always $0.00.
- **W6** — Workflow shortcut: any partial bill sets PO status `Completed`.
- **W7** — XML docs/tests say `Ordered` while enum/submit path is `Submitted`.
- **W8** — `PurchaseInvoiceDto` exposes no `RowVersion` → cancel concurrency token unusable via API.
- **W9** — `InvoiceAlreadyExists` error path dead; `BillNumber` has no unique index.
- **W10** — Idempotency docs say stored status echoed; filter forces 200; no receipt-replay integration test.

Suggestions from the report (5 items: currency-branch test, BY-02 $0.00 assertion, `BillNumber` uniqueness decision, frontend test runner, plan §1 DDL alignment) carry forward as non-blocking follow-ups.

## Layout-Mapping Note

This repo does NOT use `openspec/changes/…`; the change space is `.specify\modules\` (user-approved fork, decided this session):

- OpenSpec `openspec/specs/{domain}/spec.md` maps to `.specify\specs\04-buying\spec.md`.
- OpenSpec `openspec/changes/{change-name}/` maps to `.specify\modules\04-buying\` (pre-archive).
- OpenSpec `openspec/changes/archive/YYYY-MM-DD-{change-name}/` maps to `.specify\modules\archive\2026-10-03-04-buying\`.
- `openspec/config.yaml` does not exist → `rules.archive` not applicable.

## Traceability

- Repository HEAD at verification: `bdca3466` (verify run made no edits/commits).
- Observation IDs: not applicable (artifact store = `openspec`; artifacts are filesystem files, not Engram observations).
