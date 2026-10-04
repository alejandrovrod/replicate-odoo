using Erp.Domain.Common;

namespace Erp.Domain.Entities;

/// <summary>
/// A commercial buyer (ubiquitous language: "Customer" - .specify/modules/03-selling/spec.md §1:
/// "Commercial buyer entity holding credit terms, tax ID, currency, and default Accounts
/// Receivable account"). Physical schema per plan.md §1 (the authoritative DDL).
/// </summary>
/// <remarks>
/// <para>Company-scoped: like <see cref="Account"/> / <see cref="Warehouse"/>, a customer belongs
/// to ONE company of the tenant (<c>CompanyId NOT NULL</c>, plan.md §1), so reads are
/// company-filtered in the repository while the tenant filter stays global
/// (Constitution Article II.3).</para>
/// <para>System-versioned like Account and Company: plan.md §1 DDL literal
/// <c>WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.CustomerHistory))</c>, so credit and
/// contact changes keep a full history.</para>
/// <para><c>RowVersion</c> (spec SL-06 optimistic concurrency on credit exposure) coexists with
/// the temporal period exactly as on <see cref="Account"/> - rowversion is a regular column, the
/// period stays datetime2.</para>
/// </remarks>
public class Customer : ITenantEntity
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant; immutable after creation (Constitution Article II.4).</summary>
    public Guid TenantId { get; set; }

    /// <summary>Company that sells to this customer (plan.md §1 FK_Customer_Company).</summary>
    public Guid CompanyId { get; set; }

    /// <summary>Customer code, unique per tenant+company (plan.md §1 UQ_Customer_Tenant_Company_Code).</summary>
    public string CustomerCode { get; set; } = string.Empty;

    /// <summary>Display name (max 150 chars, plan.md §1).</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Tax identifier (max 50 chars, NOT NULL - empty string when the buyer has none, plan.md §1).</summary>
    public string TaxId { get; set; } = string.Empty;

    /// <summary>
    /// Default Accounts Receivable account for this customer's invoices (plan.md §1
    /// FK_Customer_Account); null falls back to the company-level default at posting time.
    /// </summary>
    public Guid? DefaultReceivableAccountId { get; set; }

    /// <summary>
    /// Maximum allowed exposure (spec §1 "Credit Limit": Outstanding Debt + Unbilled Delivery +
    /// Pending Order). 0.0000 means NO credit control for this customer (plan.md §2
    /// <c>CreditLimit &lt;= 0</c> short-circuit); the CHECK constraint keeps it non-negative.
    /// </summary>
    public decimal CreditLimit { get; set; }

    /// <summary>
    /// When set, <see cref="Services.CreditControlEvaluator"/> never rejects an exposure for this
    /// customer (spec SL-02: rejected only when <c>BypassCreditLimitCheck == false</c>).
    /// </summary>
    public bool BypassCreditLimitCheck { get; set; }

    /// <summary>ISO 4217 billing currency (3 chars, default 'USD', plan.md §1).</summary>
    public string BillingCurrency { get; set; } = "USD";

    /// <summary>Net payment terms in days (default 30, plan.md §1).</summary>
    public int PaymentTermsDays { get; set; } = 30;

    /// <summary>
    /// Outstanding debt already booked against this customer (spec SL-01: "the Customer's
    /// outstanding debt increases by ..."). Participates in credit exposure together with the
    /// attempted amount (spec SL-02).
    /// </summary>
    public decimal OutstandingAmount { get; set; }

    /// <summary>Inactive customers cannot be referenced by new sales documents.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optimistic concurrency token (SQL Server <c>rowversion</c> - spec SL-06: two branch users
    /// issuing invoices concurrently must resolve to exactly one winner): EF puts the original
    /// value in the UPDATE ... WHERE clause, so a concurrent change between the load and the save
    /// throws <c>DbUpdateConcurrencyException</c> instead of silently winning.
    /// Store-generated: never set from code.
    /// </summary>
    public byte[] RowVersion { get; set; } = null!;

    public Company? Company { get; set; }

    public Account? DefaultReceivableAccount { get; set; }
}
