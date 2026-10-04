# Archive Report — 01-accounting

**Change**: 01-accounting
**Date**: 2026-10-04
**Mode**: openspec (artifact store) / repo-local (planning home)
**Change root (before archive)**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\01-accounting`
**Archived to**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\archive\2026-10-04-01-accounting\`
**Outcome**: Archived as `intentional-with-warnings`

## Gate Results

| Gate | Result | Evidence |
|------|--------|----------|
| Task Completion Gate | PASS | `tasks.md`: 11/11 implementation tasks checked `[x]` (1.1–1.4, 2.1–2.7), 0 unchecked |
| Verify verdict | PASS WITH WARNINGS | Re-verification at HEAD `1a17c866`: build 0 warnings / 0 errors (verifier-run); orchestrator full suite 370/370 at this exact code state (86 Domain + 231 Application + 53 Integration); verifier scoped re-runs 144/144 |
| CRITICAL issues | 0 — none (C1 closed by scope amendment, see below) | Report states "CRITICAL: None"; 0 UNTESTED, 0 FAILING in-scope rows |
| Archive override | `intentional-with-warnings` | Standing user order for the 01/02/03 retro-archive, with the 04-buying and 02-stock precedents (`intentional-with-warnings` after reviewing a PASS WITH WARNINGS report); 9 open warnings acknowledged below |
| Action context guard | PASS | `mode: repo-local`, all operations inside `allowedEditRoots: C:\Workspace\Odoo\aspire-erp` |

## C1 Remediation Record (FAIL → PASS WITH WARNINGS)

The first verification of this module returned **Verdict FAIL** with one CRITICAL: scenario AC-05 (Period Closing Voucher) plus invariants AC-05 (Realized FX Gain/Loss) and AC-06 (period-closing roll-forward) had **no implementation and no tests anywhere in the repo**, while the spec header claimed "100% PRODUCTION CERTIFIED". The orchestrator confirmed the finding independently (repo-wide grep for `PeriodClosing|RetainedEarnings|FiscalYear|RealizedExchange|ExchangeGainLoss` → 0 hits in `*.cs`; the concepts appear only in spec/plan documents and the root roadmap glossary).

Per the user's explicit decision (amend-spec-to-reality over implement-the-gaps or archive-with-override), commit `0fa7ee1a` reconciled the contract instead of building two missing features (fiscal-year closing + FX posting — a module-sized scope that was never in any task):

1. Invariant AC-05, invariant AC-06 and scenario AC-05 now carry inline `DEFERRED — scope amendment 2026-10-04 (retro-verify)` markers and are OUT of the certified scope; history preserved, nothing deleted.
2. The spec header now reads "CERTIFIED — scope amended 2026-10-04 …" instead of the false "100% PRODUCTION CERTIFIED".
3. A second, independent re-verification at HEAD `1a17c866` re-checked every original finding against the amended spec and returned **PASS WITH WARNINGS, 0 CRITICAL** — 9/10 in-scope rows COMPLIANT, 1 PARTIAL (AC-06 idempotency, unit + sibling-endpoint coverage only), 3 rows OUT OF SCOPE. The full re-verification is this folder's `verify-report.md`.

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| 01-accounting | Created (copy — main spec did not exist) | `.specify\modules\01-accounting\spec.md` → `.specify\specs\01-accounting\spec.md`; module spec is both delta and full spec (v2.0.0 + 2026-10-04 scope amendment, 6 invariants AC-01..06 with AC-05/AC-06 DEFERRED, 7 scenarios AC-01..07 with AC-05 DEFERRED); no merge performed, no existing main spec overwritten |

## File Operations Executed (orchestrator, in order)

1. Create `.specify\specs\01-accounting\`
2. Copy `.specify\modules\01-accounting\spec.md` → `.specify\specs\01-accounting\spec.md` (spec sync BEFORE move)
3. Write `verify-report.md` and `archive-report.md` inside the change root
4. Flip `tasks.md` status line `IN PROGRESS` → certified (W5 fix, in the same commit)
5. Move `.specify\modules\01-accounting\` → `.specify\modules\archive\2026-10-04-01-accounting\` (contains spec.md, plan.md, tasks.md, verify-report.md, archive-report.md)
6. Repair the 01-accounting row in the root index tables (`.specify\plan.md`, `.specify\spec.md`, `.specify\tasks.md`): links `./modules/01-accounting/…` → `./modules/archive/2026-10-04-01-accounting/…` AND certification `100% CERTIFIED` → `CERTIFIED — amended scope` (W4 fix)
7. Repair the 01-accounting artifact paths in `.specify\scripts\agent-prompts.md` (same move)

Post-conditions confirmed after execution: main spec exists and is byte-identical (SHA-256 equal) to the archived `spec.md`; `modules\01-accounting` no longer exists; archive folder contains exactly the 5 artifacts; archived `tasks.md` still shows 11/11 `[x]`.

## Archive Contents Checklist

- [x] spec.md — functional specification v2.0.0 + scope amendment (source of truth, also synced to `.specify\specs\01-accounting\spec.md`)
- [x] plan.md — technical design (unchanged; known drift recorded as W7–W9)
- [x] tasks.md — 11/11 implementation tasks complete
- [x] verify-report.md — PASS WITH WARNINGS (re-verification at `1a17c866`)
- [x] archive-report.md — this file
- [ ] proposal.md — MISSING (by design; see below)

## Missing Artifacts Record

- **proposal.md**: absent. This module's workflow starts at spec/plan (no separate proposal phase artifact was produced); same condition recorded for 04-buying and 02-stock. Reported per skill rule "Missing proposal/spec/design artifacts should be reported."
- **applyProgress file**: absent as a standalone artifact; this repo tracks apply progress as task checkboxes inside `tasks.md` (11/11 `[x]`).

## Deferred Scope — Carry-Forward Flags (explicit, per the amendment text)

- **FX invariant (AC-05)**: no exchange-rate/settlement/FX posting path; no scenario exercises FX.
- **Period-closing invariant (AC-06) + scenario AC-05**: no fiscal-year handling, no closing voucher; account `3100 - Retained Earnings` not present in the seeded chart (suggestion S4 folded into this flag: seed it when closing lands, with `Company.RetainedEarningsAccountId`).
- These three rows are OUT of the certified scope and excluded from compliance scoring; re-certify if/when implemented.

## Warnings Acknowledgment (verify report, non-blocking)

Verdict **PASS WITH WARNINGS**, 0 CRITICAL. Archive accepted with these acknowledged warnings (W2–W10 open; previous C1 and W1 resolved by the amendment):

- **W2** — AC-06 idempotency PARTIAL: no journal-entry replay test; create endpoint unguarded (S1/S6 carry the one-test close).
- **W3** — Spec literals (`PostingToGroupAccountProhibited`) vs wire snake_case codes (deliberate, documented mapping).
- **W4** — Root index over-claim fixed in this commit (`CERTIFIED — amended scope`); module header already fixed by the amendment.
- **W5** — `tasks.md` status line fixed in this commit.
- **W6** — Duplicate `AC-xx` identifiers across invariants/scenarios.
- **W7** — Plan lists `FiscalYear.cs` (now consistent with DEFERRED scope, but still presented as delivered topology) and missing `JournalEntryForm.tsx`.
- **W8** — Plan §3 single-shot submit contract vs implemented two-step command.
- **W9** — Plan §2 Company GUID FKs vs implemented `*AccountCode` strings (`RoundOffAccountId` has neither code nor deferral).
- **W10** — Unamended glossary rows (Fiscal Year, Period Closing Voucher, Payment Ledger Entry) and column-only Cost Center.

Suggestions S1–S3, S5–S6 carry forward as non-blocking follow-ups.

## Layout-Mapping Note

This repo does NOT use `openspec/changes/…`; the change space is `.specify\modules\` (user-approved fork, decided in the 04-buying archive session):

- OpenSpec `openspec/specs/{domain}/spec.md` maps to `.specify\specs\01-accounting\spec.md`.
- OpenSpec `openspec/changes/{change-name}/` maps to `.specify\modules\01-accounting\` (pre-archive).
- OpenSpec `openspec/changes/archive/YYYY-MM-DD-{change-name}/` maps to `.specify\modules\archive\2026-10-04-01-accounting\`.
- `openspec/config.yaml` does not exist → `rules.archive` not applicable.

## Traceability

- Repository HEAD at re-verification: `1a17c866` (verify run made no edits/commits; `git status --porcelain` empty).
- Scope-amendment commit: `0fa7ee1a` (`docs(sdd): amend 01-accounting spec to mark deferred scope`).
- Observation IDs: not applicable (artifact store = `openspec`; artifacts are filesystem files, not Engram observations). Related Engram observations: `#187` (amend-vs-implement decision record).
