---
name: spec-recursive-validator
description: >-
  Recursive 3-attempt specification validation and hardening agent for GitHub Spec Kit.
  Audits, patches, and certifies ERPNext-grade specs, plans, and tasks for 100% reliability.
---

# Recursive Specification Validator Agent (3-Pass Convergence Loop)

This skill governs the autonomous 3-attempt recursive validation cycle designed to elevate module specifications (`spec.md`, `plan.md`, `tasks.md`) to 100% production-ready, rock-solid engineering standards.

---

## The 3-Attempt Recursive Protocol

```
[Target Module Triad]
        │
        ▼
┌──────────────────────────────────────────────┐
│  PASS 1: Deep Forensic Audit & Gap Matrix     │
│  - Audit against 6 Rigor Dimensions          │
│  - Identify BLOCKER, CRITICAL, WARNING gaps  │
└──────────────────────┬───────────────────────┘
                       │ Gap List
                       ▼
┌──────────────────────────────────────────────┐
│  PASS 2: Surgical Patching & Spec Hardening   │
│  - Inject missing edge cases & Gherkin       │
│  - Tighten DDL, indexes, and constraints     │
│  - Formalize formulas, CQRS, & error codes   │
└──────────────────────┬───────────────────────┘
                       │ Hardened Triad
                       ▼
┌──────────────────────────────────────────────┐
│  PASS 3: Adversarial Audit & Certification   │
│  - Verify zero contradictions                │
│  - Validate 100% invariant coverage          │
│  - Emit Zero-Defect Certification Badge      │
└──────────────────────────────────────────────┘
```

---

## 6 Rigor Dimensions Evaluated

1. **ERPNext Domain Fidelity & Accounting Invariants**:
   - Double-entry equality ($\sum \text{Debit} == \sum \text{Credit}$).
   - Perpetual inventory FIFO costing queue and stock ledger alignment with General Ledger.
   - Accrual accounting (Interim Liabilities on Receipt vs Invoice clearance).
   - Realized FX gain/loss on multi-currency settlement.
   - Temporal versioning on master data (`SYSTEM_VERSIONING = ON`).

2. **Gherkin Completeness & Boundary Coverage**:
   - Standard business scenarios (Draft -> Submitted -> Cancelled).
   - Rejection and failure modes (Insufficient stock, credit limit breach, period lock).
   - Idempotency guarantees (Duplicate submission with same `Idempotency-Key`).
   - Concurrency and race conditions (Optimistic concurrency via `RowVersion` or Row locks).

3. **Technical Architecture & SQL Server 2025 DDL**:
   - Primary Keys (`NEWSEQUENTIALID()`), Foreign Keys with cascading rules.
   - CHECK constraints for positive quantities and debit/credit amounts.
   - Composite non-clustered indexes covering tenant and status queries.
   - Temporal tables with system-versioned history.

4. **CQRS & API Boundary Contracts**:
   - Explicit Command and Query payloads with strict validation.
   - Typed responses with RFC 7807 Problem Details for all domain errors.
   - Enumerated and documented Domain Error Codes (e.g., `STOCK_INSUFFICIENT_QTY`).

5. **Atomic, Testable Tasks Checklist**:
   - Sequenced tasks mapped to Domain, Infrastructure, Application, and UI.
   - Explicit Acceptance Criteria for unit and integration tests.

6. **Cross-Module Topological Integrity**:
   - Referential integrity across modules (e.g. Stock -> Accounting, Selling -> Stock).

---

## Execution Loop Instructions

1. **Select Target**: Specify the module folder (e.g. `.specify/modules/02-stock/`).
2. **Execute Pass 1**: Produce a detailed markdown Gap Assessment table.
3. **Execute Pass 2**: Modify `spec.md`, `plan.md`, and `tasks.md` using surgical edits.
4. **Execute Pass 3**: Perform final adversarial verification. If 100% compliant, stamp:
   `> **STATUS: 100% VERIFIED & CERTIFIED (Pass 3/3)**`.
