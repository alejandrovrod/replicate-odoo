# Archive Report — 06-manufacturing

**Change**: 06-manufacturing
**Date**: 2026-10-04
**Mode**: openspec (artifact store) / repo-local (planning home)
**Change root (before archive)**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\06-manufacturing`
**Archived to**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\archive\2026-10-04-06-manufacturing\`
**Outcome**: Archived as `intentional-with-warnings`

## Gate Results

| Gate | Result | Evidence |
|------|--------|----------|
| Task Completion Gate | PASS | `tasks.md`: 7/7 implementation tasks checked `[x]` (9.1–9.7), 0 unchecked |
| Verify verdict | PASS WITH WARNINGS | Independent verify at HEAD: build 0 warnings / 0 errors; full suite 598/598 (174 Domain + 357 Application + 67 Integration); manufacturing-targeted 68 + 46 + 7 live; frontend `tsc + vite build` clean, oxlint 0 errors |
| CRITICAL issues | 0 — none | Report states "CRITICAL: None"; 0 UNTESTED, 0 PARTIAL across 3 invariants + 6 scenarios |
| Archive override | `intentional-with-warnings` | Standing user order for Spec Kit module archives, with the 01–05 precedents; 4 warnings consciously accepted below |
| Action context guard | PASS | `mode: repo-local`, all operations inside `allowedEditRoots: C:\Workspace\Odoo\aspire-erp` |

## Build Record

- Block A (9.1 workstation + 9.2 BOM/cost engine): `a2e226fb`, `dbb2c1a1` — incl. orchestrator fix of a runtime EF-model break the executor left (getter-only computed `HourRateTotal` fails model validation; unit tests never build the model — standing rule earned: every EF config change needs a live-model proof).
- Block B (9.3 work orders + 9.4 WIP/manufacture + controller): `bae4cb51`, `d12fd4e6`, `2d9063cd`
- Block C (9.5 UI + 9.6 cancel/transitive + 9.7 tests/migration/seeds): `478f9efe`, `06577c36`, `f69a65a1`, `e0e78e7b`, `9997cb9a`
- Suite growth across the module: 477/477 (pre-manufacturing) → 522 (A) → 575 (B) → 598 (C)

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| 06-manufacturing | Created (copy — main spec did not exist) | `.specify\modules\06-manufacturing\spec.md` → `.specify\specs\06-manufacturing\spec.md`; module spec v2.0.0 + verified status line (3 invariants MF-01..03, 6 scenarios MF-01..06, no deferrals); no merge performed, no existing main spec overwritten |

## File Operations Executed (orchestrator, in order)

1. Fix spec status header IN PROGRESS → IMPLEMENTED & VERIFIED (W1 fix, in the same commit)
2. Append plan §4 As-Built Addendum v1.1.0 (W3 fix — append-only, approved §§1–3 untouched)
3. Create `.specify\specs\06-manufacturing\`
4. Copy `.specify\modules\06-manufacturing\spec.md` → `.specify\specs\06-manufacturing\spec.md` (spec sync BEFORE move)
5. Write `verify-report.md` and `archive-report.md` inside the change root
6. Move `.specify\modules\06-manufacturing\` → `.specify\modules\archive\2026-10-04-06-manufacturing\` (contains spec.md, plan.md, tasks.md, verify-report.md, archive-report.md)
7. Repair the 06-manufacturing row in the root index tables (`.specify\plan.md`, `.specify\spec.md`, `.specify\tasks.md`): links `./modules/06-manufacturing/…` → `./modules/archive/2026-10-04-06-manufacturing/…` (certification `100% CERTIFIED` kept — earned: 0 UNTESTED, 0 PARTIAL)

Post-conditions confirmed after execution: main spec exists and is byte-identical (SHA-256 equal) to the archived `spec.md`; `modules\06-manufacturing` no longer exists; archive folder contains exactly the 5 artifacts; archived `tasks.md` still shows 7/7 `[x]`.

## Archive Contents Checklist

- [x] spec.md — functional specification v2.0.0 + verified status (source of truth, also synced to `.specify\specs\06-manufacturing\spec.md`)
- [x] plan.md — technical design 1.0.0 + §4 As-Built Addendum v1.1.0
- [x] tasks.md — 7/7 implementation tasks complete, IMPLEMENTATION COMPLETE status
- [x] verify-report.md — PASS WITH WARNINGS
- [x] archive-report.md — this file
- [ ] proposal.md — MISSING (by design; see below)

## Missing Artifacts Record

- **proposal.md**: absent. This module's workflow starts at spec/plan (no separate proposal phase artifact was produced); same condition as 01–05. Reported per skill rule "Missing proposal/spec/design artifacts should be reported."
- **applyProgress file**: absent as a standalone artifact; progress tracked as task checkboxes inside `tasks.md` (7/7 `[x]`) plus per-block commits.

## Warnings Acknowledgment (verify report — consciously accepted)

- **W1** — Spec status header fixed in this commit (was stale "IN PROGRESS — Block A…").
- **W2** — No BOM write path: MF-01's "engineer submits the BOM" step is satisfied by construction (domain-engine literals + live consumption), not by execution — `BomsController` is honestly read-only, no command exists, out of scope by design. Accepted.
- **W3** — Plan deltas documented via the §4 As-Built Addendum in this commit (codes, naming, BomOperation table, IsDefault gate, cancel semantics, scrap posture, read-only BOM API). Approved §§1–3 untouched.
- **W4** — RowVersion contention unexercised under concurrent transitions (configured + mapped, catch paths mirror tested modules; MF-06 proof is the stock-level race, which is the spec's actual scenario). Accepted as low-risk observation.

Suggestions carried forward: S1 (scrap-bearing manufacture is a hard wall — deferred design input: scrap GL account/flow; engine computes, posting rejects); S2 (test-oracle robustness commentary, self-guarding assertions); S3 (oxlint warnings outside this module — follow-up cleanup so future lint output is warning-free).

## Deferred / Carry-Forward Flags

- None deferred in scope (all 9 spec rows certified). Cross-module notes: JobCard/actual operator time stays deferred (planned-ops absorption basis documented); partial multi-voucher staging deferred (single `TransferStockEntryId` link); BOM-create command/UI deferred (SQL-seeded masters).

## Layout-Mapping Note

This repo does NOT use `openspec/changes/…`; the change space is `.specify\modules\` (user-approved fork, decided in the 04-buying archive session):

- OpenSpec `openspec/specs/{domain}/spec.md` maps to `.specify\specs\06-manufacturing\spec.md`.
- OpenSpec `openspec/changes/{change-name}/` maps to `.specify\modules\06-manufacturing\` (pre-archive).
- OpenSpec `openspec/changes/archive/YYYY-MM-DD-{change-name}/` maps to `.specify\modules\archive\2026-10-04-06-manufacturing\`.
- `openspec/config.yaml` does not exist → `rules.archive` not applicable.

## Traceability

- Repository HEAD at verification: full suite 598/598 (verifier-run; orchestrator runs at each block close corroborate the growth 477 → 522 → 575 → 598).
- Implementation commits: Blocks A/B/C as listed above; Engram observations `#192` (Block A), `#193` (Block B), `#194` (Block C).
- Observation IDs: not applicable (artifact store = `openspec`; artifacts are filesystem files, not Engram observations).
