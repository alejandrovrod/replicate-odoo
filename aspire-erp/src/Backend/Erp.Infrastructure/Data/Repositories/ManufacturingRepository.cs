using System.Data;
using System.Data.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IManufacturingRepository"/>: workstation and BOM
/// persistence plus the Block B (Tasks 9.3/9.4) work-order workflow and its gapless WO number
/// generator (Constitution III.4). No manual <c>.Where(e =&gt; e.TenantId == ...)</c> on LINQ
/// queries (Constitution II.3 - the global query filter does it); the raw SQL voucher statement
/// scopes by TenantId itself because EF query filters do not apply to raw SQL.
/// </summary>
public sealed class ManufacturingRepository : IManufacturingRepository
{
    private const string WorkOrderTable = "dbo.WorkOrder";
    private const string WorkOrderColumn = "OrderNumber";

    private readonly AppDbContext _dbContext;

    public ManufacturingRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // Join an already-open transaction instead of creating a nested one (same DbContext instance).
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    /// <summary>
    /// SELECT MAX(OrderNumber) WITH (UPDLOCK, HOLDLOCK) inside the AMBIENT posting transaction,
    /// scoped to (TenantId, CompanyId, prefix-year) - the same Constitution III.4 generator the
    /// purchase and stock repositories use, so a rolled-back work order consumes NO number.
    /// </summary>
    public async Task<string> NextWorkOrderNumberAsync(
        Guid companyId, string prefix, int year, CancellationToken cancellationToken = default)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Voucher numbering must run inside the posting transaction (Constitution III.4): "
                + "outside one the UPDLOCK/HOLDLOCK range lock cannot protect the sequence.");

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        var tenantId = _dbContext.CurrentTenantId;
        var pattern = $"{prefix}-{year}-%";

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT MAX({WorkOrderColumn}) FROM {WorkOrderTable} WITH (UPDLOCK, HOLDLOCK) "
            + $"WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND {WorkOrderColumn} LIKE @Pattern;";
        command.Transaction = transaction.GetDbTransaction();

        AddParameter(command, "@TenantId", tenantId);
        AddParameter(command, "@CompanyId", companyId);
        AddParameter(command, "@Pattern", pattern);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var max = scalar as string;

        var nextSequence = 1;
        if (!string.IsNullOrEmpty(max))
        {
            var separator = max.LastIndexOf('-');
            if (separator < 0 || !int.TryParse(max[(separator + 1)..], out var currentSequence))
            {
                throw new InvalidOperationException(
                    $"Stored voucher number '{max}' does not follow the PREFIX-YYYY-NNNNN format.");
            }

            if (currentSequence >= 99999)
            {
                throw new InvalidOperationException(
                    $"Voucher sequence for prefix '{prefix}' and year {year} is exhausted.");
            }

            nextSequence = currentSequence + 1;
        }

        return $"{prefix}-{year}-{nextSequence:00000}";
    }

    public async Task<Workstation?> GetWorkstationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.Workstations.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<BillOfMaterials?> GetBomByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.BillsOfMaterials
            .Include(e => e.Items)
            .Include(e => e.Operations)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<WorkOrder?> GetWorkOrderByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.WorkOrders.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task AddWorkstationAsync(Workstation workstation, CancellationToken cancellationToken = default)
    {
        await _dbContext.Workstations.AddAsync(workstation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddBomAsync(BillOfMaterials bom, CancellationToken cancellationToken = default)
    {
        await _dbContext.BillsOfMaterials.AddAsync(bom, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        await _dbContext.WorkOrders.AddAsync(workOrder, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateWorkOrderAsync(WorkOrder workOrder, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Spec MF-06: another request transitioned the order between our load and this save -
            // the RowVersion WHERE clause matched 0 rows. Translate into the typed domain failure
            // the handlers convert into a 409 Result.Failure, mirroring the purchase repository.
            throw new ConcurrencyConflictException(nameof(WorkOrder), workOrder.Id, ex);
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
