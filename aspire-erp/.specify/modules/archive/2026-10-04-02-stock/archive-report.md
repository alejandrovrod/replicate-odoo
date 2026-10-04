# Archive Report — 02-stock

**Change**: 02-stock
**Date**: 2026-10-04
**Mode**: openspec (artifact store) / repo-local (planning home)
**Change root (before archive)**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\02-stock`
**Archived to**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\archive\2026-10-04-02-stock\`
**Outcome**: Archived as `intentional-with-warnings`

## Gate Results

| Gate | Result | Evidence |
|------|--------|----------|
| Task Completion Gate | PASS | `tasks.md`: 9/9 implementation tasks checked `[x]` (3.1–3.9), 0 unchecked |
| Verify verdict | PASS WITH WARNINGS | Re-verification at HEAD `99f5d518`: build 0 warnings / 0 errors; 370/370 tests passed (86 Domain + 231 Application + 53 Integration); frontend `tsc -b && vite build` clean; exit code 0 |
| CRITICAL issues | 0 — none (C1 closed, see remediation below) | Report states "CRITICAL: None"; 0 UNTESTED, 0 FAILING rows (was 1 UNTESTED at first verify) |
| Archive override | `intentional-with-warnings` | Standing user order for the 01/02/03 retro-archive, with the 04-buying precedent (`intentional-with-warnings` after reviewing a PASS WITH WARNINGS report); 9 non-critical warnings acknowledged below |
| Action context guard | PASS | `mode: repo-local`, all operations inside `allowedEditRoots: C:\Workspace\Odoo\aspire-erp` |

## C1 Remediation Record (FAIL → PASS WITH WARNINGS)

The first verification of this module returned **Verdict FAIL** with one CRITICAL: scenario ST-04 (stock movement cancellation, Task 3.7) had **zero tests** — `CancelStockEntryCommandHandler` had no unit coverage and no test called `POST /api/v1/stockentries/{id}/cancel`. The orchestrator confirmed the finding independently (grep over `tests/` found only purchase-invoice and journal-entry cancellations) and, while closing it, proved the problem was **deeper than untested**:

1. **Root cause**: `CancelStockEntryCommandHandler` was never registered in `Erp.Api/Program.cs`. The composition root registers handlers explicitly (one `AddScoped<ICommandHandler<…>, …>` per handler), and `Sender.DispatchAsync` throws `InvalidOperationException` on an unregistered handler (`ISender.cs:47-50`) → the cancel route returned **HTTP 500 on every call since delivery**. Unit tests passed because they instantiate the handler directly, bypassing DI entirely.
2. **Fix + coverage** (commit `2460d84e`): the missing registration (1 line at `Program.cs:77`), plus 6 handler unit tests (`CancelStockEntryCommandHandlerTests`) and 3 live HTTP integration tests (`StockEntriesCancellationApiTests`: restoration with history kept, double-cancel 409, unknown voucher 404).
3. **Re-verification**: a second, independent verify run at HEAD `99f5d518` re-checked every original finding, audited all 42 handlers in the composition root for the same defect class (none missing), re-ran build + full suite + targeted ST-04 tests, and returned **PASS WITH WARNINGS, 0 CRITICAL, 0 UNTESTED**. The full re-verification is this folder's `verify-report.md`.

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| 02-stock | Created (copy — main spec did not exist) | `.specify\modules\02-stock\spec.md` → `.specify\specs\02-stock\spec.md`; module spec is both delta and full spec (v2.0.0, 4 invariants ST-01..04, 6 scenarios ST-01..06); no merge performed, no existing main spec overwritten |

## File Operations Executed (orchestrator, in order)

1. Create `.specify\specs\02-stock\`
2. Copy `.specify\modules\02-stock\spec.md` → `.specify\specs\02-stock\spec.md` (spec sync BEFORE move)
3. Write `verify-report.md` and `archive-report.md` inside the change root
4. Move `.specify\modules\02-stock\` → `.specify\modules\archive\2026-10-04-02-stock\` (contains spec.md, plan.md, tasks.md, verify-report.md, archive-report.md)
5. Repair the 02-stock artifact links in the root index tables (`.specify\plan.md`, `.specify\spec.md`, `.specify\tasks.md`): `./modules/02-stock/…` → `./modules/archive/2026-10-04-02-stock/…`

Post-conditions confirmed after execution: main spec exists and is byte-identical (SHA-256 equal) to the archived `spec.md`; `modules\02-stock` no longer exists; archive folder contains exactly the 5 artifacts; archived `tasks.md` still shows 9/9 `[x]`.

## Archive Contents Checklist

- [x] spec.md — functional specification v2.0.0 (source of truth, also synced to `.specify\specs\02-stock\spec.md`)
- [x] plan.md — technical design 1.0.0
- [x] tasks.md — 9/9 implementation tasks complete
- [x] verify-report.md — PASS WITH WARNINGS (re-verification at `99f5d518`)
- [x] archive-report.md — this file
- [ ] proposal.md — MISSING (by design; see below)

## Missing Artifacts Record

- **proposal.md**: absent. This module's workflow starts at spec/plan (no separate proposal phase artifact was produced); same condition recorded for 04-buying. Reported per skill rule "Missing proposal/spec/design artifacts should be reported."
- **applyProgress file**: absent as a standalone artifact; this repo tracks apply progress as task checkboxes inside `tasks.md` (9/9 `[x]`).

## Warnings Acknowledgment (verify report, non-blocking)

Verdict **PASS WITH WARNINGS**, 0 CRITICAL. Archive accepted with these acknowledged warnings (all re-checked in the re-verification):

- **W1** — `spec.md:81` ST-04 first clause ("original `StockLedgerEntry` marked `IsCancelled = 1`") contradicts Constitution III.2; impl and tests flag the reversal rows instead. Spec sentence is stale.
- **W2** — Spec ST-02 names COGS `5120`; the chart uses `5210` (`5120` is Purchase Price Difference).
- **W3** — Test comments cite a phantom "spec §4 ST-02" with different literals than spec v2.0.0 §3.
- **W4** — Task 3.1 acceptance clause (warehouse accounts resolving through hierarchy to company defaults) not implemented.
- **W5** — Identical-warehouse transfer rejection implemented but untested.
- **W6** — ST-03 exception literal differs from the thrown message (structured fields asserted instead).
- **W7** — No test replays `POST /api/v1/stockentries` itself (sibling endpoints prove the filter).
- **W8** — Append-only enforcement covers `GLEntry` only; `StockLedgerEntry` immutability is documented but not guarded by trigger/EF checks.
- **W9** — ST-01 portfolio clause (GL stock-asset balance == Σ closing Kardex values) untested.

Suggestions from the report (S1–S9, including the `tasks.md` Status line flip, spec §2 `AllowNegativeStock` documentation, plan drift refresh, spec status-line downgrade, two one-liner tests, frontend test runner, SLE append-only trigger, literal GL-assertion on cancel, and a reflection-based DI-registration guard) carry forward as non-blocking follow-ups.

## Layout-Mapping Note

This repo does NOT use `openspec/changes/…`; the change space is `.specify\modules\` (user-approved fork, decided in the 04-buying archive session):

- OpenSpec `openspec/specs/{domain}/spec.md` maps to `.specify\specs\02-stock\spec.md`.
- OpenSpec `openspec/changes/{change-name}/` maps to `.specify\modules\02-stock\` (pre-archive).
- OpenSpec `openspec/changes/archive/YYYY-MM-DD-{change-name}/` maps to `.specify\modules\archive\2026-10-04-02-stock\`.
- `openspec/config.yaml` does not exist → `rules.archive` not applicable.

## Traceability

- Repository HEAD at re-verification: `99f5d518` (verify run made no edits/commits; `git status --porcelain` empty).
- Remediation commit: `2460d84e` (`fix(stock): wire stock entry cancellation endpoint and cover ST-04`); index-link commit for this archive included in this change's commit.
- Observation IDs: not applicable (artifact store = `openspec`; artifacts are filesystem files, not Engram observations). Related Engram observation: `#186` (bugfix record of the DI root cause).
