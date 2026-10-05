# Functional Specification: CRM & Sales Pipeline (ERPNext Parity)

**Module:** `08-crm`  
**Status:** IMPLEMENTED & VERIFIED - 7/7 tasks,
PASS WITH WARNINGS (0 CRITICAL, 0 UNTESTED)  
**Version:** 2.0.0  
**Methodology:** Domain-Driven Design (DDD) & GitHub Spec Kit  
**Canonical Reference:** [ERPNext CRM Documentation](https://docs.frappe.io/erpnext/CRM)  

---

## 1. Executive Summary & Ubiquitous Language

The **CRM Module** captures prospect interest, qualifies commercial demand, tracks salesperson activities, and manages the pre-sales pipeline from initial **Lead** capture to qualified **Opportunity** and seamless conversion into a **Quotation**, **Customer**, and **Sales Order** within the Selling Module.

| Ubiquitous Term | ERPNext DocType | Definition & Invariants |
| :--- | :--- | :--- |
| **Lead** | `Lead` | Unqualified individual or organization expressing interest in company products or services. |
| **Opportunity** | `Opportunity` | A qualified commercial deal actively pursued, with estimated monetary value, closing date, and probability. |
| **Sales Stage** | `Sales Stage` | Milestone in the sales cycle: Prospecting (10%), Qualification (25%), Value Proposition (50%), Negotiation (80%), Closed Won (100%), Closed Lost (0%). |
| **Probability** | `Opportunity.probability` | Percentage likelihood of winning the deal used to forecast weighted pipeline revenue. |
| **Weighted Amount** | Weighted Value | Forecasted revenue calculated as $\text{OpportunityAmount} \times (\text{Probability} / 100)$. |
| **Lead Conversion** | Convert Lead | Action converting a qualified lead into an Opportunity and/or formal Customer record while maintaining communication audit history. |
| **Loss Reason** | `Opportunity.loss_reason` | Mandatory classification capturing why a deal was marked Closed Lost. |

---

## 2. Core Business Invariants & Pipeline Rules

### Invariant CRM-01: Weighted Forecast Accuracy
For any pipeline forecasting calculation:
$$\text{WeightedForecast} = \sum_{i=1}^{n} \text{OpportunityAmount}_i \times \frac{\text{Probability}_i}{100.0}$$
$$\text{WeightedAmount}_{\text{ClosedWon}} = \text{OpportunityAmount} \times 1.0000, \quad \text{WeightedAmount}_{\text{ClosedLost}} = 0.0000$$
- Opportunities in `ClosedWon` have $\text{Probability} = 100\%$ ($\text{Weighted} == \text{Amount}$).
- Opportunities in `ClosedLost` have $\text{Probability} = 0\%$ ($\text{Weighted} == 0.00$).

### Invariant CRM-02: Mandatory Loss Reason Requirement
An `Opportunity` cannot transition to `ClosedLost` without providing a non-empty `LossReason`.
- Violations throw `CRMValidationException("crm_loss_reason_required")`.

### Invariant CRM-03: Seamless Conversion Traceability
When converting a `Lead` into a `Customer` or `Opportunity`:
1. The original `Lead` status becomes `Converted`.
2. Communication logs and activity notes copy or reference the newly created deal.
3. The original lead record is never deleted, preserving attribution to marketing campaigns.

---

## 3. Gherkin Functional Scenarios

### Scenario CRM-01: Inbound Lead Capture and Conversion to Opportunity
- **Given** an inbound web enquiry from `Alex Rivera` at `TechCorp`
- **When** the sales team qualifies the lead and clicks "Convert to Opportunity"
- **Then** `Lead.Status` becomes `Converted`
- **And** a new `Opportunity` is created with `Stage = Qualification`, `Probability = 25%`, and amount $15,000.00
- **And** the weighted pipeline value increases by $3,750.00.

### Scenario CRM-02: Stage Progression to Closed Won
- **Given** open Opportunity `OPP-2026-0042` with amount $50,000.00 at `Negotiation` (80% probability)
- **When** the customer accepts the commercial proposal
- **Then** the opportunity moves to `ClosedWon`
- **And** `Probability` is set to 100% (weighted amount = $50,000.00)
- **And** the UI enables 1-click creation of a formal `SalesOrder`.

### Scenario CRM-03: Enforce Loss Reason on Lost Deal
- **Given** an active Opportunity with proposal submitted
- **When** the salesperson marks the deal as `ClosedLost` without specifying a reason
- **Then** the operation is rejected with `CRMValidationException` (`crm_loss_reason_required`, HTTP 400)
- **When** the user provides `LossReason = "Competitor priced 15% lower"`
- **Then** status becomes `ClosedLost` with `Probability = 0%`.

### Scenario CRM-04: Idempotent Lead Ingestion Guard
- **Given** an inbound webhook from an external campaign with `Idempotency-Key: idemp-lead-2026-99`
- **When** the webhook is retried due to gateway timeout
- **Then** the idempotency filter identifies the existing lead record
- **And** returns HTTP 200 with the previously created `LeadDto`
- **And** strictly prevents duplicate lead records in the database.

### Scenario CRM-05: Deal Cancellation & Re-opening Workflow
- **Given** an opportunity marked `ClosedLost` with a recorded loss reason
- **When** the client contacts the sales team months later to resume negotiations
- **Then** the opportunity can be re-opened to `Negotiation` stage
- **And** probability updates to 50%
- **And** historical loss reason is preserved in the audit history.

### Scenario CRM-06: Concurrency Guard on Simultaneous Opportunity Stage Update
- **Given** opportunity `OPP-2026-0042` open in two browser tabs
- **When** two team members simultaneously update stages to different milestones
- **Then** optimistic concurrency locks via `RowVersion` detect the collision
- **And** the first save succeeds while the second receives `ConcurrencyConflictException`.

