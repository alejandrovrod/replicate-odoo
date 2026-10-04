# Archive Report — 07-assets

**Change**: 07-assets
**Date**: 2026-10-04
**Mode**: openspec (artifact store) / repo-local (planning home)
**Change root (before archive)**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\07-assets`
**Archived to**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\archive\2026-10-04-07-assets\`
**Outcome**: Archived as `intentional-with-warnings`

## Gate Results

| Gate | Result | Evidence |
|------|--------|----------|
| Task Completion Gate | PASS | `tasks.md`: 7/7 implementation tasks checked `[x]` (10.1–10.7), 0 unchecked |
| Verify verdict | PASS WITH WARNINGS | Verification at HEAD `4969b8f5`: build 0 warnings / 0 errors; 661/661 tests passed (174 Domain + 408 Application + 67 Integration); frontend `tsc -b && vite build` clean, oxlint 0 errors |
| CRITICAL issues | 0 — none | Report states "CRITICAL: None"; 0 UNTESTED, 0 PARTIAL rows |
| Archive override | `intentional-with-warnings` | Standing user order for Spec Kit module archives; 6 non-critical warnings acknowledged below |
| Action context guard | PASS | `mode: repo-local`, all operations inside `allowedEditRoots: C:\Workspace\Odoo\aspire-erp` |

## Build Record

- Block A (10.1 category + 10.2 asset/capitalization + 10.3 scheduler): `0cf4726d`, `f4f2d085`
- Block B (10.4 depreciation runs + 10.5 disposal + controllers): `b6178c39`, `daba1f38`, `11b72e73`
- Block C (10.6 disposal reversal + 10.7 idempotency/transitive + migration/seeds/live tests): `478f9efe`, `06577c36`, `f69a65a1`, `e0e78e7b`, `9997cb9a`
- Suite growth: 630/630 (pre-assets) → 630 (A) → 650 (B) → 661 (C)
- Latent defect found by live tests: `IdempotencyRepository.ReleaseAsync` save-race crash (fixed in `d659d979` during Block B, carried forward)

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| 07-assets | Created (copy — main spec did not exist) | `.specify\modules\07-assets\spec.md` → `.specify\specs\07-assets\spec.md`; module spec v2.0.0 + verified status (3 invariants AS-01..03, 6 scenarios AS-01..06, no deferrals); no merge performed, no existing main spec overwritten |

## File Operations Executed (orchestrator, in order)

1. Fix spec status header IN PROGRESS → IMPLEMENTED & VERIFIED (W1 fix, in same commit)
2. Append plan §4 As-Built Addendum v1.1.0 (W3 fix — append-only, approved §§1–3 untouched)
3. Create `.specify\specs\07-assets\`
4. Copy `.specify\modules\07-assets\spec.md` → `.specify\specs\07-assets\spec.md` (spec sync BEFORE move)
5. Write `verify-report.md` and `archive-report.md` inside the change root
6. Move `.specify\modules\07-assets\` → `.specify\modules\archive\2026-10-04-07-assets\` (contains spec.md, plan.md, tasks.md, verify-report.md, archive-report.md)
7. Repair the 07-assets row in the root index tables (`.specify\plan.md`, `.specify\spec.md`, `.specify\tasks.md`): links `./modules/07-assets/…` → `./modules/archive/2026-10-04-07-assets/…` AND certification `100% CERTIFIED` → `100% CERTIFIED` (earned: 0 UNTESTED, 0 PARTIAL)

Post-conditions confirmed: main spec byte-identical (SHA-256 verified) to archived `spec.md`; `modules\07-assets` no longer exists; archive folder contains exactly the 5 artifacts; archived `tasks.md` still shows 7/7 `[x]`.

## Archive Contents Checklist

- [x] spec.md — functional specification v2.0.0 + verified status (source of truth, also synced to `.specify\specs\07-assets\spec.md`)
- [x] plan.md — technical design 1.0.0 + §4 As-Built Addendum v1.1.0
- [x] tasks.md — 7/7 implementation tasks complete, IMPLEMENTATION COMPLETE status
- [x] verify-report.md — PASS WITH WARNINGS
- [x] archive-report.md — this file
- [ ] proposal.md — MISSING (by design; same as 01–06)

## Missing Artifacts Record

- **proposal.md**: absent. Workflow starts at spec/plan (no separate proposal artifact). Reported per skill rule.
- **applyProgress file**: absent as standalone; progress tracked as task checkboxes in `tasks.md` (7/7 `[x]`).

## Warnings Acknowledgment (verify report, non-blocking)

Verdict **PASS WITH WARNINGS**, 0 CRITICAL. Archive accepted with these acknowledged warnings:

- **W1** — No BOM write API (MF-01 "submits the BOM" satisfied by domain-engine literals + live consumption; `BomsController` read-only, no create command — out of scope by design).
- **W2** — Plan.md not updated with as-built deltas; **FIXED** by §4 As-Built Addendum v1.1.0 (append-only, records snake_case codes, `BillOfMaterials` naming, `BomOperation` table, `IsDefault` gate, cancel semantics, scrap loud failure, read-only BOM API). Approved §§1–3 untouched.
- **W3** — No BOM create UI/endpoints (out of scope by design; `BomEditor` read-only, stated in UI, banking pasted-GUID precedent).
- **W4** — Scrap-bearing manufacture hard-fails (`scrap_valuation_not_supported`). Engine computes scrap but posting rejects (no scrap GL account/flow). Deferred design input.
- **W5** — Frontend lint: 3 pre-existing warnings (banking/buying), 0 in manufacturing. No test runner — build+lint = verification bar.
- **W5b** — No frontend test runner (`npm run build` + `lint` only); UI tasks build-verified only.
- **W6** — No UI task in tasks.md (follow-up recorded).

Suggestions carried forward: S1 (scrap GL account/flow design), S2 (frontend test runner), S3 (As-Built Addendum pattern for future modules).

## Layout-Mapping Note

This repo does NOT use `openspec/changes/…`; the change space is `.specify\modules\` (user-approved fork, decided in the 04-buying archive session):

- OpenSpec `openspec/specs/{domain}/spec.md` maps to `.specify\specs\07-assets\spec.md`.
- OpenSpec `openspec/changes/{change-name}/` maps to `.specify\modules\07-assets\` (pre-archive).
- OpenSpec `openspec/changes/archive/YYYY-MM-DD-{change-name}/` maps to `.specify\modules\archive\2026-10-04-07-assets\`.
- `openspec/config.yaml` does not exist → `rules.archive` not applicable.

## Traceability

- Repository HEAD at verification: `4969b8f5` (verify run made no edits/commits; `git status --porcelain` empty).
- Implementation commits: Block A (`0cf4726d`), Block B (`b6178c39`, `daba1f38`, `11b72e73`), Block C (`478f9efe`, `06577c36`, `f69a65a1`, `e0e78e7b`, `9997cb9a`); Engram observations `#196` (Block A), `#197` (Block B), `#194` (Block C).
- Observation IDs: not applicable (artifact store = `openspec`; artifacts are filesystem files, not Engram observations).