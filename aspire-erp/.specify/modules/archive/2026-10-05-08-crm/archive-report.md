# Archive Report — 08-crm

**Change**: 08-crm
**Date**: 2026-10-05
**Mode**: openspec (artifact store) / repo-local (planning home)
**Change root (before archive)**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\08-crm`
**Archived to**: `C:\Workspace\Odoo\aspire-erp\.specify\modules\archive\2026-10-05-08-crm\`
**Outcome**: Archived as `intentional-with-warnings`

## Gate Results

| Gate | Result | Evidence |
|------|--------|----------|
| Task Completion Gate | PASS | `tasks.md`: 7/7 implementation tasks checked `[x]` (11.1–11.7), 0 unchecked |
| Verify verdict | PASS WITH WARNINGS | Independent re-verify at HEAD `6d684745`: build 0 errors; full suite 812/812 (242 Domain + 489 Application + 81 Integration); CRM-targeted 44 + 7 + 9 live + 14 selling; vite CRM chunk emitted, tsc 0 errors, lint 0 errors |
| CRITICAL issues | 0 — none | Report states "CRITICAL — none"; 0 UNTESTED, 0 PARTIAL across 10 spec rows |
| Prior round | FAIL → fix-pass → PASS | First verify FAIL (C1 unreachable board, C2 unbuilt SalesOrder clause); fix-pass closed both + all warnings with live evidence (`065eb53e`, `3d679e75`, `6d684745`); re-verify PASS WITH WARNINGS |
| Archive override | `intentional-with-warnings` | Standing user order for Spec Kit module archives, with the 01–07 and 09 precedents; 4 warnings (2 fixed at archive, 2 carried forward) |
| Action context guard | PASS | `mode: repo-local`, all operations inside `allowedEditRoots: C:\Workspace\Odoo\aspire-erp` |

## Build Record

- Adoptive Block A (adopt lead/opportunity base + CRMActivity + EF + DI, re-verified 11.1–11.3/11.5/11.7-unit, reopened 11.4/11.6/11.7-integration): `ba9a297c`, `d30a4ac0`
- Block B (AdvanceStage + webhook dedup + conversion activity wiring + controllers, re-ticked 11.4/11.6): `b574fb1c`, `56ac6934`, `9bd3ae81`
- Block C (migration + seeds + 5 live tests + Kanban rewiring to live endpoints, ticked 11.7): `912c88ac`, `dcb5f763`, `b100b9c1`
- Fix-pass (C1 reachable board + C2 sales-order action + dedup unique index + convert-default alignment + reopen guard, 812/812): `065eb53e`, `3d679e75`, `6d684745`
- Migrations `20261005045803_AddCrmModule` (Lead/Opportunity temporal + CRMActivity) and `20261005053823_AddLeadDedupUniqueIndex` (filtered `UQ_Lead_Company_Source_ExternalRef`) generated via `dotnet ef`, correct `Migrations/` placement, both applied to dev DB; `scripts/seed-dev-crm.sql` idempotent + applied
- Suite growth across the module: 766/766 (pre-CRM) → 790/790 (Blocks A–C) → 795/795 (Block C live) → 812/812 (fix-pass)

## Specs Synced

| Domain | Action | Details |
|--------|--------|---------|
| 08-crm | Created (copy — main spec did not exist) | `.specify\modules\08-crm\spec.md` → `.specify\specs\08-crm\spec.md`; module spec v2.0.0 + verified status + error-code wording fix (`CRMValidationException` / `crm_loss_reason_required`); no merge performed, no existing main spec overwritten |

## File Operations Executed (orchestrator, in order)

1. Fix spec status header (W-SPEC-HEADER) + error-code wording (W-SPEC-CODES, 2 lines: invariant CRM-02 + scenario CRM-03) + tasks status/11.5/11.7 closure notes (W-NEW), in the same commit
2. Create `.specify\specs\08-crm\`
3. Copy `.specify\modules\08-crm\spec.md` → `.specify\specs\08-crm\spec.md` (spec sync BEFORE move)
4. Write `verify-report.md` and `archive-report.md` inside the change root
5. Move `.specify\modules\08-crm\` → `.specify\modules\archive\2026-10-05-08-crm\` (contains spec.md, plan.md, tasks.md, verify-report.md, archive-report.md)
6. Repair the 08-crm row in the root index tables (`.specify\spec.md`, `.specify\plan.md`, `.specify\tasks.md`): links `./modules/08-crm/…` → `./modules/archive/2026-10-05-08-crm/…`, certification → `CERTIFIED — with warnings`

Post-conditions confirmed after execution: main spec exists and is byte-identical (SHA-256 equal) to the archived `spec.md`; `modules\08-crm` no longer exists; archive folder contains exactly the 5 artifacts; archived `tasks.md` still shows 7/7 `[x]`.

## Archive Contents Checklist

- [x] spec.md — functional specification v2.0.0 + verified status + error-code wording fix (source of truth, also synced to `.specify\specs\08-crm\spec.md`)
- [x] plan.md — technical design 1.0.0 (unchanged; error-catalog drift recorded, not rewritten post-hoc)
- [x] tasks.md — 7/7 implementation tasks complete, IMPLEMENTED & VERIFIED status with fix-pass closure notes
- [x] verify-report.md — PASS WITH WARNINGS (re-verification round)
- [x] archive-report.md — this file
- [ ] proposal.md — MISSING (by design; same as 01–07, 09)

## Missing Artifacts Record

- **proposal.md**: absent. Workflow starts at spec/plan (no separate proposal artifact); same condition as 01–07, 09. Reported per skill rule.
- **applyProgress file**: absent as standalone; progress tracked as checkboxes in `tasks.md` (7/7 `[x]`) plus per-block commits.

## Warnings Acknowledgment (verify report)

- **W-NEW** — `create-sales-order` endpoint rides implicitly under 11.5/CRM-02 (tests cover it; tasks text names it only via the fix-pass closure note added at archive). Accepted scope; no new box required.
- **W-SPEC-HEADER** — Spec status header fixed in this commit (was stale "IN PROGRESS — Block A … Block B/C pending").
- **W-SPEC-CODES** — Spec error-code wording fixed in this commit (`CRMValidationException("crm_loss_reason_required")`); spec now matches certified reality.
- **W-PLAN** — Plan §2 error-catalog namespace/values disagree with shipped code (documented deviation, behavior stable + live-locked). Plan left unchanged per no-post-hoc-rewrite rule. Accepted low-risk follow-up.

Suggestions carried forward: `OpportunityId` linkage FK on SalesOrder, entity-level reopen probability guard, buying/banking lint warnings (owning modules).

## Deferred / Carry-Forward Flags

- None deferred in scope (all 10 spec rows have executed proofs; 0 PARTIAL).
- Cross-module notes: `CreateOpportunitySalesOrderCommandHandler` delegates to the existing selling handler (CRM owns preconditions only); linkage is response + Note activity (no `OpportunityId` FK — recorded honestly).

## Layout-Mapping Note

This repo does NOT use `openspec/changes/…`; the change space is `.specify\modules\` (user-approved fork, decided in the 04-buying archive session):

- OpenSpec `openspec/specs/{domain}/spec.md` maps to `.specify\specs\08-crm\spec.md`.
- OpenSpec `openspec/changes/{change-name}/` maps to `.specify\modules\08-crm\` (pre-archive).
- OpenSpec `openspec/changes/archive/YYYY-MM-DD-{change-name}/` maps to `.specify\modules\archive\2026-10-05-08-crm\`.
- `openspec/config.yaml` does not exist → `rules.archive` not applicable.

## Traceability

- Repository HEAD at verification: `6d684745`, full suite 812/812 (verifier-run; orchestrator runs corroborate the growth 766 → 812).
- Implementation commits: adoptive A + B + C + fix-pass as listed above; Engram observations `#206` (Block C), `#207` (fix-pass).
- Observation IDs: not applicable (artifact store = `openspec`; artifacts are filesystem files, not Engram observations).
