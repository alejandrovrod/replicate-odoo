using System.Linq.Expressions;
using Erp.Application.Common;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Entities.System;
using Erp.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Erp.Infrastructure.Data;

/// <summary>
/// Enterprise DbContext (plan.md §2.2). Dynamically registers a global query filter on every
/// tenant-scoped entity (Constitution Article II.3 - manual .Where(e => e.TenantId == ...) in
/// application services is forbidden) and enforces TenantId immutability plus fail-closed insert
/// rules on save (Article II.4).
/// </summary>
public class AppDbContext : DbContext
{
    private const string TenantIdPropertyName = nameof(ITenantEntity.TenantId);

    private readonly ITenantProvider _tenantProvider;

    public AppDbContext(
        DbContextOptions<AppDbContext> options,
        ITenantProvider tenantProvider) : base(options)
    {
        _tenantProvider = tenantProvider ?? throw new ArgumentNullException(nameof(tenantProvider));
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    /// <summary>Global ISO currency catalog (RM-09): shared across tenants, no tenant filter.</summary>
    public DbSet<Currency> Currencies => Set<Currency>();

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<PeriodClosingVoucher> PeriodClosingVouchers => Set<PeriodClosingVoucher>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<UOM> UOMs => Set<UOM>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<Warehouse> Warehouses => Set<Warehouse>();

    public DbSet<StockEntry> StockEntries => Set<StockEntry>();

    public DbSet<StockLedgerEntry> StockLedgerEntries => Set<StockLedgerEntry>();

    public DbSet<GLEntry> GLEntries => Set<GLEntry>();

    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();

    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();

    public DbSet<PurchaseInvoice> PurchaseInvoices => Set<PurchaseInvoice>();

    public DbSet<SalesOrder> SalesOrders => Set<SalesOrder>();

    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();

    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
    
    public DbSet<POSProfile> POSProfiles => Set<POSProfile>();

    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();

    public DbSet<BankAccount> BankAccounts => Set<BankAccount>();

    public DbSet<BankStatementImport> BankStatementImports => Set<BankStatementImport>();

    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();

    public DbSet<PaymentEntry> PaymentEntries => Set<PaymentEntry>();

    public DbSet<PaymentAllocation> PaymentAllocations => Set<PaymentAllocation>();

    public DbSet<BankTransactionRule> BankTransactionRules => Set<BankTransactionRule>();

    public DbSet<BankReconciliation> BankReconciliations => Set<BankReconciliation>();

    public DbSet<Workstation> Workstations => Set<Workstation>();

    public DbSet<BillOfMaterials> BillsOfMaterials => Set<BillOfMaterials>();

    public DbSet<BomItem> BomItems => Set<BomItem>();

    public DbSet<BomOperation> BomOperations => Set<BomOperation>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<AssetCategory> AssetCategories => Set<AssetCategory>();

    public DbSet<Asset> Assets => Set<Asset>();

    public DbSet<AssetDepreciationSchedule> AssetDepreciationSchedules => Set<AssetDepreciationSchedule>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Designation> Designations => Set<Designation>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<SalaryComponent> SalaryComponents => Set<SalaryComponent>();

    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();

    public DbSet<SalaryStructureLine> SalaryStructureLines => Set<SalaryStructureLine>();

    public DbSet<SalaryStructureAssignment> SalaryStructureAssignments => Set<SalaryStructureAssignment>();

    public DbSet<PayrollEntry> PayrollEntries => Set<PayrollEntry>();

    public DbSet<SalarySlip> SalarySlips => Set<SalarySlip>();

    public DbSet<SalarySlipLine> SalarySlipLines => Set<SalarySlipLine>();

    public DbSet<Lead> Leads => Set<Lead>();

    public DbSet<Opportunity> Opportunities => Set<Opportunity>();

    public DbSet<CRMActivity> CRMActivities => Set<CRMActivity>();

    public DbSet<Catalog> Catalogs => Set<Catalog>();
    
    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    
    public DbSet<CatalogItemTranslation> CatalogItemTranslations => Set<CatalogItemTranslation>();

    /// <summary>Tenant visible to this context instance. Exposed for model cache keying if needed.</summary>
    public Guid CurrentTenantId => _tenantProvider.GetCurrentTenantId();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        // One model per tenant: without this, the cached model keeps the FIRST context's
        // ITenantProvider and every later scope queries with a stale tenant (proved by the probe).
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, TenantModelCacheKeyFactory>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Constitution Article II.1 + II.3: every mapped entity either implements ITenantEntity
        // (gets the filter) or carries no TenantId at all. A Guid TenantId WITHOUT the contract
        // would silently bypass filtering (a data leak), so it fails the model build instead.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var clrType = entityType.ClrType;
            var implementsContract = IsTenantScoped(clrType);

            if (!implementsContract
                && clrType.GetProperty(TenantIdPropertyName)?.PropertyType == typeof(Guid))
            {
                throw new InvalidOperationException(
                    $"Entity '{clrType.FullName}' declares a {TenantIdPropertyName} property "
                    + $"but does not implement {nameof(ITenantEntity)}. Tenant-scoped entities "
                    + "must implement ITenantEntity (Constitution Article II.1); otherwise the "
                    + "global query filter would not apply and the entity would leak across "
                    + "tenants.");
            }

            if (!implementsContract)
            {
                continue;
            }

            var parameter = Expression.Parameter(clrType, "e");
            var tenantIdProperty = Expression.Property(parameter, TenantIdPropertyName);

            // GetCurrentTenantId is a METHOD, so the plan.md snippet's Expression.Property(...) would
            // throw at model build time; Expression.Call keeps the invocation inside the expression
            // tree so EF evaluates it at query execution time rather than baking in a value.
            var currentTenantId = Expression.Call(
                Expression.Constant(_tenantProvider),
                typeof(ITenantProvider).GetMethod(nameof(ITenantProvider.GetCurrentTenantId))!);

            var filter = Expression.Lambda(
                Expression.Equal(tenantIdProperty, currentTenantId),
                parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    /// <summary>
    /// Constitution Article II.3 (literal): an entity is tenant-scoped when and only when it
    /// implements <see cref="ITenantEntity"/>. An entity declaring a <c>Guid TenantId</c> without
    /// the contract is rejected at model build time (see <see cref="OnModelCreating"/>) so the
    /// filter can never be silently skipped.
    /// </summary>
    private static bool IsTenantScoped(Type clrType) =>
        typeof(ITenantEntity).IsAssignableFrom(clrType);

    /// <summary>
    /// Constitution Article II.4 (literal): iterate the tracked <see cref="ITenantEntity"/>
    /// entries - newly inserted ones receive CurrentTenantId automatically (throws when no tenant
    /// context exists - fail closed); altering TenantId of an existing entity throws instead of
    /// silently reverting it (plan.md's <c>IsModified = false</c> sample is overridden by the
    /// Constitution).
    /// </summary>
    private void EnforceTenantInvariants()
    {
        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    if (!_tenantProvider.HasTenant())
                    {
                        throw new InvalidOperationException(
                            $"Cannot add entity '{entry.Metadata.Name}': no tenant context is set. "
                            + "Tenant-scoped rows can only be created inside a resolved tenant (fail closed).");
                    }

                    entry.Property(e => e.TenantId).CurrentValue = _tenantProvider.GetCurrentTenantId();
                    break;

                case EntityState.Modified:
                    var originalValue = entry.Property(e => e.TenantId).OriginalValue;
                    var currentValue = entry.Property(e => e.TenantId).CurrentValue;
                    if (!Equals(originalValue, currentValue))
                    {
                        throw new InvalidOperationException(
                            $"Altering TenantId of existing entity '{entry.Metadata.Name}' from "
                            + $"'{originalValue}' to '{currentValue}' is forbidden (Constitution Article II.4).");
                    }

                    break;
            }
        }
    }

    /// <summary>
    /// Constitution Article III.2 (literal): GLEntry is INSERT-ONLY. Any Modified or Deleted
    /// tracked GLEntry entry aborts the save by throwing - the Constitution also forbids the old
    /// plan.md sample that silently cleared <c>IsModified</c>, so the ONLY legal handling is to
    /// fail (the database trigger <c>trg_GLEntry_AppendOnly</c> backs this up for raw SQL).
    /// </summary>
    private void EnforceLedgerAppendOnly()
    {
        foreach (var entry in ChangeTracker.Entries<GLEntry>())
        {
            if (entry.State == EntityState.Modified)
            {
                throw new GLEntryAppendOnlyViolationException("UPDATE");
            }

            if (entry.State == EntityState.Deleted)
            {
                throw new GLEntryAppendOnlyViolationException("DELETE");
            }
        }
    }

    // Overriding the (acceptAllChangesOnSuccess, cancellationToken) overload is sufficient: the
    // parameterless SaveChanges/SaveChangesAsync overloads funnel into these virtual methods.
    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnforceLedgerAppendOnly();
        EnforceTenantInvariants();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnforceLedgerAppendOnly();
        EnforceTenantInvariants();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
