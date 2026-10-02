# Project Constitution: Multi-Tenant Cloud ERP Core

**Status:** APPROVED & MANDATORY  
**Target Platform:** .NET Aspire (.NET 9/10), Microsoft SQL Server 2025, React 19 + TypeScript  
**Architectural Model:** Clean Architecture (Onion / Hexagonal)  

This Constitution defines the non-negotiable architectural laws, engineering standards, and invariants that govern all AI agents and human engineers contributing to this codebase. Any code, pull request, or design that violates these rules must be rejected.

---

## Article I: Clean Architecture Invariant

1. **Dependency Inversion Law:** Code dependencies flow strictly inwards:
   $$\text{Domain} \longleftarrow \text{Application} \longleftarrow \text{Infrastructure / Api}$$
2. **Pure Domain Isolation:** The `Erp.Domain` project must contain **zero external dependencies** (no EF Core, no ASP.NET Core, no third-party libraries except standard BCL). Domain entities, value objects, domain events, and domain exceptions reside here.
3. **Application Layer Contracts:** The `Erp.Application` project depends exclusively on `Domain`. It defines CQRS commands, queries, DTOs, domain service interfaces, and FluentValidation rules.
4. **Infrastructure Layer:** The `Erp.Infrastructure` project implements database persistence (`AppDbContext`), external gateways, and compile-time logging. It never exposes internal database models to the `Api` layer.

---

## Article II: Multi-Tenant Perimeter & Isolation

1. **Tenant Identification:** Every tenant-scoped entity must implement `ITenantEntity` (`Guid TenantId { get; set; }`).
2. **Context-Injected DbContext:** Tenant resolution occurs in a Scoped middleware (`ITenantProvider`) and is injected into `AppDbContext` at instantiation.
3. **Automated Query Isolation:** EF Core Global Query Filters (`HasQueryFilter`) must be dynamically registered on all `ITenantEntity` models via expression trees. Writing manual `.Where(e => e.TenantId == ...)` in application services is forbidden; query filtering is automatic.
4. **Immutability of Tenant Identity:** In `SaveChangesAsync()`, any attempt to alter `TenantId` on an existing entity must throw an `InvalidOperationException`. Newly inserted entities automatically receive `CurrentTenantId`.

---

## Article III: Financial Ledger Invariants (Double-Entry Law)

1. **Partida Doble (Zero-Sum Invariant):** Every financial voucher posted to the General Ledger (`GLEntry`) must satisfy:
   $$\sum_{i=1}^{n} \text{Debit}_i - \sum_{i=1}^{n} \text{Credit}_i = 0.0000$$
   If an entry does not balance down to four decimal places, the transaction must immediately throw `DoubleEntryImbalanceException` and roll back.
2. **Immutability of the Ledger:** Records in `GLEntry` are **INSERT-ONLY**. `UPDATE` and `DELETE` operations on `GLEntry` are strictly prohibited at both ORM and database trigger levels.
3. **Reversal Correction Law:** Errors or cancellations must be recorded as compensatory reversal entries with equal opposite debit and credit lines, referencing the original voucher.
4. **Sequential Gapless Numbering:** Fiscal document numbers (e.g. `SINV-2026-00001`) must be generated sequentially within an isolated transaction lock per company and calendar year.

---

## Article IV: Database Standards (Microsoft SQL Server 2025)

1. **Composite Tenant Indexing:** Every index on a tenant-scoped table must place `TenantId` as the leading column (e.g., `IX_GLEntry_Tenant_Account_Date ON GLEntry (TenantId, AccountId, PostingDate)`).
2. **Forensic Audit via Temporal Tables:** Master entities (`Account`, `Company`) must enable SQL Server 2025 System-Versioned Temporal Tables (`SYSTEM_VERSIONING = ON`) to retain historical audit trails.
3. **Positive Decimal Constraints:** All monetary debit and credit fields must enforce `CHECK (Debit >= 0)` and `CHECK (Credit >= 0)`.

---

## Article V: Logging & Observability Standards

1. **Ambient Multi-Tenant Scopes:** All inbound HTTP requests must establish an `ILogger.BeginScope` containing `TenantId`, `CorrelationId`, and `UserId`.
2. **Zero-Allocation Source Generation:** High-frequency log methods must utilize the `[LoggerMessage]` attribute on partial static methods. Runtime string interpolation (`$"..."`) inside logger calls is prohibited.
3. **OpenTelemetry Compliance:** All services must hook into `Erp.ServiceDefaults` to export traces, metrics, and logs to the .NET Aspire Dashboard.

---

## Article VI: REST API & Controller Attributes

1. **Explicit Routing & Versioning:** Controllers must declare `[ApiController]`, `[Route("api/v1/[controller]")]`, and `[Authorize(Policy = "TenantMember")]`.
2. **Content Negotiation:** Every controller must declare `[Produces("application/json")]` and `[Consumes("application/json")]`.
3. **Exhaustive OpenAPI Documentation:** Every action method must document its HTTP status codes using `[ProducesResponseType]`.
4. **Idempotency Guard:** Any mutation endpoint executing ledger postings must require the `[IdempotencyKeyRequired]` filter.

---

## Article VII: Frontend Standards (React 19 + TypeScript + Shadcn UI)

1. **Feature-Driven Structure:** Business code must be encapsulated in `src/features/<feature_name>/` (components, hooks, pages).
2. **Stateless UI Primitives:** The `src/components/ui/` folder contains only presentation primitives from Radix UI and Tailwind CSS.
3. **Axios Centralization:** All network communication must pass through `src/api/client.ts` with automated `X-Tenant-ID` injection and RFC 7807 `ProblemDetails` error handling.
4. **Predictable State:** Global state is managed with Zustand stores. Complex forms (e.g., invoice line items) must compute taxes and totals reactively in real time.
