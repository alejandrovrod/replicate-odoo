using System.Data;
using System.Data.Common;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data.Pagination;
using Microsoft.Data.SqlClient;
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
public sealed class CrmRepository : ICrmRepository, ICrmActivityRepository, IQuotationRepository, ICrmCatalogRepository
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
        => _dbContext.Opportunities
            .Include(o => o.Items)
            .Include(o => o.Activities)
            .Include(o => o.Quotations)
            .FirstOrDefaultAsync(o => o.Id == opportunityId, cancellationToken);

    public Task<Lead?> GetLeadByDedupKeyAsync(Guid companyId, string source, string externalReference, CancellationToken cancellationToken = default)
        => _dbContext.Leads.FirstOrDefaultAsync(
            l => l.CompanyId == companyId && l.Source == source && l.ExternalReference == externalReference,
            cancellationToken);

    public async Task<PagedResult<Lead>> ListLeadsAsync(Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default)
        => await _dbContext.Leads
            .Where(l => l.CompanyId == companyId)
            .OrderByDescending(l => l.Id)
            .ToPagedResultAsync(paging, cancellationToken);

    public async Task<PagedResult<Opportunity>> ListOpportunitiesAsync(Guid companyId, PagedRequest paging, string? stage, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Opportunities
            .Where(o => o.CompanyId == companyId);

        // The stage filter used to run in memory after Take(); it now constrains the query so
        // pages stay consistent (Standard Pagination Pattern).
        if (!string.IsNullOrWhiteSpace(stage))
        {
            query = query.Where(o => o.Stage == stage);
        }

        return await query
            .OrderByDescending(o => o.OpportunityNumber)
            .ToPagedResultAsync(paging, cancellationToken);
    }

    public async Task AddLeadAsync(Lead lead, CancellationToken cancellationToken = default)
    {
        await _dbContext.Leads.AddAsync(lead, cancellationToken);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Fix-pass W1: UQ_Lead_Company_Source_ExternalRef is the hard backstop of the
            // CRM-04 replay rule - the handler's GetLeadByDedupKeyAsync pre-check can be raced
            // by a concurrent ingest, so translate the race into a typed domain failure instead
            // of leaking an EF/SQL exception (the CustomerRepository.AddAsync precedent). The
            // handler re-reads the winner and answers Duplicate=true.
            _dbContext.Entry(lead).State = EntityState.Detached;
            throw new DuplicateLeadException(lead.CompanyId, lead.Source, lead.ExternalReference!, ex);
        }
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
        // Explicit root marking (NOT Update()): the header row must ALWAYS UPDATE with its
        // RowVersion predicate - an undetected root turns a lost optimistic race into a
        // phantom 200 (spec CRM-06). Unlike Update(), this touches ONLY the root: child
        // collections keep their own states (explicit RemoveRange/AddRange in
        // ReplaceOpportunityItemsAsync), so keyed newcomers can never degrade into 0-row
        // UPDATEs. All callers load-then-mutate in scope; a detached graph still attaches
        // correctly because Entry() attaches it first.
        _dbContext.Entry(opportunity).State = EntityState.Modified;

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

    public async Task ReplaceOpportunityItemsAsync(
        Guid opportunityId,
        IReadOnlyList<OpportunityItem> lines,
        CancellationToken cancellationToken = default)
    {
        var stored = await _dbContext.OpportunityItems
            .Where(i => i.OpportunityId == opportunityId)
            .ToListAsync(cancellationToken);
        _dbContext.OpportunityItems.RemoveRange(stored);
        await _dbContext.OpportunityItems.AddRangeAsync(lines, cancellationToken);
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

    // ------------------------------------------------- quotations (ERPNext pre-order flow)

    public Task<Quotation?> GetByIdAsync(Guid quotationId, CancellationToken cancellationToken = default)
        => _dbContext.Quotations
            .Include(q => q.Items)
            .FirstOrDefaultAsync(q => q.Id == quotationId, cancellationToken);

    public async Task AddAsync(Quotation quotation, CancellationToken cancellationToken = default)
    {
        await _dbContext.Quotations.AddAsync(quotation, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Quotation>> ListQuotationsByOpportunityAsync(
        Guid opportunityId,
        CancellationToken cancellationToken = default)
        => await _dbContext.Quotations
            .Where(q => q.OpportunityId == opportunityId)
            .OrderByDescending(q => q.CreatedAt)
            .ThenByDescending(q => q.Id)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Next gapless quotation number, e.g. 2026 -> "QTN-2026-00001" (Constitution III.4:
    /// locked MAX read inside the ambient transaction - the NextOpportunityNumberAsync
    /// precedent, same UPDLOCK/HOLDLOCK discipline).
    /// </summary>
    public async Task<string> NextQuotationNumberAsync(
        Guid companyId,
        int year,
        CancellationToken cancellationToken = default)
    {
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await ReadNextDocumentNumberAsync(
                "Quotations", "QuotationNo", $"QTN-{year}-%", companyId, cancellationToken);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        var next = await ReadNextDocumentNumberAsync(
            "Quotations", "QuotationNo", $"QTN-{year}-%", companyId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return next;
    }

    // ------------------------------------------------- read-only catalogs (segmentation)

    /// <summary>
    /// Default rows seeded on first read per tenant (the catalogs are reference data: a
    /// fresh tenant must close deals on day one without an admin seeding step). The unique
    /// (TenantId, Code) index backstops the race: a concurrent ensure loses the insert and
    /// re-reads (the AddLeadAsync duplicate-lead precedent).
    /// </summary>
    private static readonly (string Code, string Name)[] DefaultLossReasons =
    [
        ("PRICE", "Lost on Price"),
        ("COMPETITOR", "Lost to Competitor"),
        ("TIMING", "Bad Timing"),
        ("BUDGET", "No Budget"),
        ("FEATURE", "Missing Features"),
        ("RELATIONSHIP", "Incumbent Relationship"),
        ("OTHER", "Other"),
    ];

    private static readonly (string Code, string Name)[] DefaultTerritories =
    [
        ("NORTH", "North"),
        ("SOUTH", "South"),
        ("EAST", "East"),
        ("WEST", "West"),
        ("CENTRAL", "Central"),
        ("INTL", "International"),
    ];

    private static readonly (string Code, string Name)[] DefaultIndustries =
    [
        ("MANUFACTURING", "Manufacturing"),
        ("RETAIL", "Retail"),
        ("HEALTHCARE", "Healthcare"),
        ("TECHNOLOGY", "Technology"),
        ("SERVICES", "Services"),
        ("CONSTRUCTION", "Construction"),
        ("OTHER", "Other"),
    ];

    public async Task<IReadOnlyList<OpportunityLossReason>> ListLossReasonsAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureCatalogDefaultsAsync(
            _dbContext.OpportunityLossReasons,
            DefaultLossReasons,
            (tenantId, code, name) => new OpportunityLossReason
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                IsActive = true,
            },
            cancellationToken);

        return await _dbContext.OpportunityLossReasons
            .Where(r => r.IsActive)
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<OpportunityLossReason?> GetLossReasonByIdAsync(
        Guid lossReasonId,
        CancellationToken cancellationToken = default)
        => _dbContext.OpportunityLossReasons
            .FirstOrDefaultAsync(r => r.Id == lossReasonId, cancellationToken);

    public async Task<IReadOnlyList<Territory>> ListTerritoriesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureCatalogDefaultsAsync(
            _dbContext.Territories,
            DefaultTerritories,
            (tenantId, code, name) => new Territory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                IsActive = true,
            },
            cancellationToken);

        return await _dbContext.Territories
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Industry>> ListIndustriesAsync(
        CancellationToken cancellationToken = default)
    {
        await EnsureCatalogDefaultsAsync(
            _dbContext.Industries,
            DefaultIndustries,
            (tenantId, code, name) => new Industry
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Code = code,
                Name = name,
                IsActive = true,
            },
            cancellationToken);

        return await _dbContext.Industries
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    private async Task EnsureCatalogDefaultsAsync<T>(
        DbSet<T> set,
        (string Code, string Name)[] defaults,
        Func<Guid, string, string, T> factory,
        CancellationToken cancellationToken)
        where T : class
    {
        if (await set.AnyAsync(cancellationToken))
        {
            return;
        }

        var tenantId = _dbContext.CurrentTenantId;
        foreach (var (code, name) in defaults)
        {
            await set.AddAsync(factory(tenantId, code, name), cancellationToken);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // A concurrent ensure won the race: detach our copies and serve the winner.
            foreach (var entry in _dbContext.ChangeTracker.Entries<T>().ToList())
            {
                entry.State = EntityState.Detached;
            }
        }
    }

    private async Task<string> ReadNextDocumentNumberAsync(
        string table,
        string column,
        string pattern,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Quotation numbering must run inside a transaction: outside one the "
                + "UPDLOCK/HOLDLOCK range lock cannot protect the sequence.");

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        var tenantId = _dbContext.CurrentTenantId;
        var prefix = pattern[..pattern.LastIndexOf('-')];

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT MAX({column}) FROM dbo.{table} WITH (UPDLOCK, HOLDLOCK) "
            + "WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND "
            + $"{column} LIKE @Pattern;";
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
                    $"Stored quotation number '{max}' does not follow the QTN-YYYY-NNNNN format.");
            }

            if (currentSequence >= 99999)
            {
                throw new InvalidOperationException(
                    $"Quotation sequence for '{prefix}' is exhausted (max 99999).");
            }

            nextSequence = currentSequence + 1;
        }

        return $"{prefix}-{nextSequence:D5}";
    }

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

    private static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
