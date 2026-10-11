using Erp.Domain.Entities.Security;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Seeders;

/// <summary>
/// Seeds the base RBAC matrix (Fase R3): the six system roles plus their
/// <see cref="DocTypePermission"/> rows, per tenant. Idempotent and safe under parallel
/// boots (check-then-insert converging on the primary key, the ReceivableAccountSeeder
/// precedent).
/// </summary>
/// <remarks>
/// <para><b>System Manager</b> owns no permission rows on purpose: the authorization handler
/// bypasses every <c>permission:</c> policy for that role (ERPNext Administrator equivalent).
/// The JWT generator flattens each row into <c>perms</c> claims shaped
/// <c>doctype:action</c>, which is exactly what the controller policies demand.</para>
///
/// <para><b>Operational reads:</b> every functional role also gets <c>company:read</c>,
/// <c>currency:read</c> and <c>catalog:read</c> - without them pickers and headers cannot
/// load and the roles would be unusable day one. Accounting additionally owns
/// <c>report:read</c>.</para>
/// </remarks>
public static class SecurityRoleSeeder
{
    public static readonly Guid SystemManagerRoleId = Guid.Parse("f0000000-0000-4000-8000-000000000001");
    public static readonly Guid SalesUserRoleId = Guid.Parse("f0000000-0000-4000-8000-000000000002");
    public static readonly Guid PurchaseUserRoleId = Guid.Parse("f0000000-0000-4000-8000-000000000003");
    public static readonly Guid AccountingUserRoleId = Guid.Parse("f0000000-0000-4000-8000-000000000004");
    public static readonly Guid HrUserRoleId = Guid.Parse("f0000000-0000-4000-8000-000000000005");
    public static readonly Guid ManufacturingUserRoleId = Guid.Parse("f0000000-0000-4000-8000-000000000006");

    private sealed record RoleSeed(Guid Id, string Name, string Description);
    private sealed record PermSeed(string DocType, bool Read, bool Write, bool Submit, bool Cancel)
    {
        public bool Create => Write;
        // No base role receives delete: destructive endpoints stay System Manager-only.
        public bool Delete => false;
    }

    private static readonly RoleSeed[] Roles =
    [
        new(SystemManagerRoleId, "System Manager", "Unrestricted access (authorization bypass)."),
        new(SalesUserRoleId, "Sales User", "Selling cycle: orders, invoices, customers, items."),
        new(PurchaseUserRoleId, "Purchase User", "Buying cycle: orders, bills, suppliers, items."),
        new(AccountingUserRoleId, "Accounting User", "Ledger, payments, banking and reports."),
        new(HrUserRoleId, "HR User", "People, attendance, payroll."),
        new(ManufacturingUserRoleId, "Manufacturing User", "BOMs, work orders, stock."),
    ];

    private static readonly Dictionary<Guid, PermSeed[]> Matrix = new()
    {
        [SalesUserRoleId] = [
            new("sales_order", true, true, true, false),
            new("sales_invoice", true, true, true, false),
            new("delivery_note", true, true, false, false),
            new("customer", true, true, true, false),
            new("lead", true, true, true, false),
            new("opportunity", true, true, true, true),
            new("item", true, true, true, false),
            new("company", true, false, false, false),
            new("currency", true, false, false, false),
            new("catalog", true, false, false, false),
        ],
        [PurchaseUserRoleId] = [
            new("purchase_order", true, true, true, false),
            new("purchase_invoice", true, true, true, false),
            new("purchase_receipt", true, true, false, false),
            new("supplier", true, true, true, false),
            new("item", true, true, true, false),
            new("company", true, false, false, false),
            new("currency", true, false, false, false),
            new("catalog", true, false, false, false),
        ],
        [AccountingUserRoleId] = [
            new("account", true, true, false, false),
            new("currency", true, true, false, false),
            new("journal_entry", true, true, true, true),
            new("payment_entry", true, true, true, true),
            new("gl_entry", true, true, true, true),
            new("bank_account", true, true, true, false),
            new("bank_transaction", true, true, true, true),
            new("bank_statement_import", true, true, false, false),
            new("bank_transaction_rule", true, true, false, false),
            new("fiscal_year", true, true, true, false),
            new("period_closing_voucher", true, true, true, true),
            new("exchange_rate", true, true, false, false),
            new("exchange_rate_revaluation", true, true, true, true),
            new("asset", true, true, true, true),
            new("asset_category", true, true, false, false),
            new("report", true, false, false, false),
            new("company", true, false, false, false),
            new("catalog", true, false, false, false),
        ],
        [HrUserRoleId] = [
            new("employee", true, true, true, false),
            new("attendance", true, true, true, false),
            new("leave_application", true, true, true, false),
            new("salary_component", true, true, false, false),
            new("salary_structure", true, true, false, false),
            new("payroll_entry", true, true, true, true),
            new("company", true, false, false, false),
            new("catalog", true, false, false, false),
        ],
        [ManufacturingUserRoleId] = [
            new("bom", true, true, true, false),
            new("work_order", true, true, true, true),
            new("project", true, true, true, false),
            new("warehouse", true, true, false, false),
            new("stock_entry", true, true, true, true),
            new("stock", true, false, false, false),
            new("item", true, true, true, false),
            new("company", true, false, false, false),
            new("catalog", true, false, false, false),
        ],
    };

    /// <summary>Ensures the six roles and their permission rows for one tenant.</summary>
    public static async Task SeedAsync(AppDbContext context, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var roleIdMap = new Dictionary<Guid, Guid>();

        foreach (var role in Roles)
        {
            var existingRole = await context.Roles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == role.Name, cancellationToken);
            
            if (existingRole == null)
            {
                existingRole = new Role
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Name = role.Name,
                    Description = role.Description,
                    IsSystemDefault = true,
                };
                context.Roles.Add(existingRole);
            }
            
            roleIdMap[role.Id] = existingRole.Id;
        }

        await SaveConvergingAsync(context, cancellationToken);

        foreach (var (hardcodedRoleId, perms) in Matrix)
        {
            var actualRoleId = roleIdMap[hardcodedRoleId];
            foreach (var perm in perms)
            {
                var exists = await context.DocTypePermissions
                    .IgnoreQueryFilters()
                    .AnyAsync(p => p.TenantId == tenantId && p.RoleId == actualRoleId && p.DocType == perm.DocType, cancellationToken);
                
                if (!exists)
                {
                    context.DocTypePermissions.Add(new DocTypePermission
                    {
                        Id = Guid.NewGuid(),
                        TenantId = tenantId,
                        RoleId = actualRoleId,
                        DocType = perm.DocType,
                        CanRead = perm.Read,
                        CanWrite = perm.Write,
                        CanCreate = perm.Create,
                        CanDelete = perm.Delete,
                        CanSubmit = perm.Submit,
                        CanCancel = perm.Cancel,
                    });
                }
            }
        }

        await SaveConvergingAsync(context, cancellationToken);
    }

    private static async Task SaveConvergingAsync(AppDbContext context, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Parallel boots may insert the same deterministic rows: detach the losers and
            // continue - the re-reads above already converge on the winners. Anything else
            // rethrows loudly instead of hiding a real failure.
            foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is Microsoft.Data.SqlClient.SqlException sql
            && (sql.Number == 2601 || sql.Number == 2627);
}
