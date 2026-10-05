using System.Data;
using System.Data.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="ICrmRepository"/> and <see cref="ICrmActivityRepository"/>
/// (Block A, tasks 11.1-11.7): lead/opportunity reads+writes, the gapless opportunity number
/// generator and the CRM follow-up log. One class implements both contracts - the
/// SalesRepository (ISalesOrderRepository + IDeliveryNoteRepository) precedent - so Block B
/// still writes through the SAME scoped AppDbContext.
/// </summary>
/// <remarks>
/// There is deliberately NO manual <c>.Where(e =&gt; e.TenantId == ...)</c> on LINQ queries
/// (Constitution II.3 - the global query filter does it). The CompanyId/OpportunityId
/// predicates are business scoping (CustomerRepository precedent), not tenancy. The ONE raw
/// SQL statement scopes TenantId explicitly because EF query filters do NOT apply to raw SQL
/// (StockRepository.NextVoucherNumberAsync precedent).
/// </remarks>
public sealed class CrmRepository : ICrmRepository, ICrmActivityRepository
{
    private readonly AppDbContext _dbContext;

    public CrmRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        // Join an already-open transaction instead of creating a nested one (StockRepository precedent).
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<Lead?> GetLeadByIdAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _dbContext.Leads.FirstOrDefaultAsync(l => l.Id == leadId, cancellationToken);

    public Task<Opportunity?> GetOpportunityByIdAsync(Guid opportunityId, CancellationToken cancellationToken = default)
        => _dbContext.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunityId, cancellationToken);

    public async Task AddLeadAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        await _dbContext.Leads.AddAsync(lead, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateLeadAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        _dbContext.Leads.Update(lead);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        await _dbContext.Opportunities.AddAsync(opportunity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateOpportunityAsync(Opportunity opportunity, CancellationToken cancellationToken = default)
    {
        _dbContext.Opportunities.Update(opportunity);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Spec CRM-06: another user moved this deal between our load and our save - the
            // RowVersion WHERE clause matched 0 rows (BankRepository.UpdateTransactionAsync precedent).
            throw new ConcurrencyConflictException(nameof(Opportunity), opportunity.Id, ex);
        }
    }

    /// <summary>
    /// Next gapless number for a company/year, e.g. 2026 -> "OPP-2026-00001" (Constitution III.4:
    /// SELECT MAX with UPDLOCK/HOLDLOCK, released only by commit/rollback, so a rolled-back
    /// conversion consumes NO number - StockRepository.NextVoucherNumberAsync precedent).
    /// </summary>
    /// <remarks>
    /// Unlike the stock voucher path this runs OUTSIDE the conversion transaction today (the
    /// adopted ConvertLeadCommandHandler numbers BEFORE opening it), so when no ambient
    /// transaction exists it opens its own short one around the locked read instead of throwing.
    /// </remarks>
    public async Task<string> NextOpportunityNumberAsync(
        Guid companyId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await ReadNextOpportunityNumberAsync(companyId, year, cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var next = await ReadNextOpportunityNumberAsync(companyId, year, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return next;
    }

    public async Task AddActivityAsync(CRMActivity activity, CancellationToken cancellationToken = default)
    {
        CRMActivityValidator.EnsureValidActivityFields(
            activity.OpportunityId,
            activity.Subject,
            activity.CreatedByUserId);

        await _dbContext.CRMActivities.AddAsync(activity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CRMActivity>> ListByOpportunityAsync(
        Guid opportunityId,
        CancellationToken cancellationToken = default)
        => await _dbContext.CRMActivities
            .Where(a => a.OpportunityId == opportunityId)
            .OrderBy(a => a.ActivityDate)
            .ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);

    private async Task<string> ReadNextOpportunityNumberAsync(
        Guid companyId,
        int year,
        CancellationToken cancellationToken)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Opportunity numbering must run inside a transaction: outside one the "
                + "UPDLOCK/HOLDLOCK range lock cannot protect the sequence.");

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        // EF's global query filter does NOT apply to raw SQL, so the tenant scope is written out
        // explicitly here (it reproduces exactly what the filter would have done).
        var tenantId = _dbContext.CurrentTenantId;
        var pattern = $"OPP-{year}-%";

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT MAX(OpportunityNumber) FROM dbo.Opportunity WITH (UPDLOCK, HOLDLOCK) "
            + "WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND OpportunityNumber LIKE @Pattern;";
        command.Transaction = transaction.GetDbTransaction();

        AddParameter(command, "@TenantId", tenantId);
        AddParameter(command, "@CompanyId", companyId);
        AddParameter(command, "@Pattern", pattern);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        var max = scalar as string;

        var nextSequence = 1;
        if (!string.IsNullOrEmpty(max))
        {
            // Fixed 5-digit zero padding keeps lexicographic and numeric order identical.
            var separator = max.LastIndexOf('-');
            if (separator < 0 || !int.TryParse(max[(separator + 1)..], out var currentSequence))
            {
                throw new InvalidOperationException(
                    $"Stored opportunity number '{max}' does not follow the OPP-YYYY-NNNNN format.");
            }

            if (currentSequence >= 99999)
            {
                throw new InvalidOperationException(
                    $"Opportunity sequence for 'OPP-{year}' is exhausted (max 99999).");
            }

            nextSequence = currentSequence + 1;
        }

        return $"OPP-{year}-{nextSequence:D5}";
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
