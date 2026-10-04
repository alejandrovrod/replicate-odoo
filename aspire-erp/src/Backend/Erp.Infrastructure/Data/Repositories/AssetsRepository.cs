using System.Data;
using System.Data.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IAssetsRepository"/>: category/asset persistence, the
/// capitalization GL writes and the gapless AST number generator (Constitution III.4). No manual
/// <c>.Where(e =&gt; e.TenantId == ...)</c> on LINQ queries (Constitution II.3 - the global query
/// filter does it); the raw SQL asset-code statement scopes by TenantId itself because EF query
/// filters do not apply to raw SQL.
/// </summary>
public sealed class AssetsRepository : IAssetsRepository
{
    private const string AssetCodePrefix = "AST";
    private const string AssetTable = "dbo.Asset";
    private const string AssetCodeColumn = "AssetCode";

    private readonly AppDbContext _dbContext;

    public AssetsRepository(AppDbContext dbContext)
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
    /// Constitution III.4: SELECT MAX(AssetCode) WITH (UPDLOCK, HOLDLOCK) inside the AMBIENT
    /// posting transaction, scoped to (TenantId, CompanyId, year). A rolled-back capitalization
    /// consumes NO number.
    /// </summary>
    public async Task<string> NextAssetCodeAsync(
        Guid companyId,
        int year,
        CancellationToken cancellationToken = default)
    {
        var transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Asset numbering must run inside the posting transaction (Constitution III.4): "
                + "outside one the UPDLOCK/HOLDLOCK range lock cannot protect the sequence.");

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await _dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        var tenantId = _dbContext.CurrentTenantId;
        var pattern = $"{AssetCodePrefix}-{year}-%";

        await using var command = connection.CreateCommand();
        command.CommandText =
            $"SELECT MAX({AssetCodeColumn}) FROM {AssetTable} WITH (UPDLOCK, HOLDLOCK) "
            + $"WHERE TenantId = @TenantId AND CompanyId = @CompanyId AND {AssetCodeColumn} LIKE @Pattern;";
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
                    $"Stored asset code '{max}' does not follow the AST-YYYY-NNNNN format.");
            }

            if (currentSequence >= 99999)
            {
                throw new InvalidOperationException(
                    $"Asset sequence for year {year} is exhausted (max 99999).");
            }

            nextSequence = currentSequence + 1;
        }

        return $"{AssetCodePrefix}-{year}-{nextSequence:D5}";
    }

    public async Task<AssetCategory?> GetCategoryByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.AssetCategories.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<Asset?> GetAssetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _dbContext.Assets.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AssetDepreciationSchedule>> GetSchedulesByAssetAsync(
        Guid assetId,
        CancellationToken cancellationToken = default)
        => await _dbContext.AssetDepreciationSchedules
            .Where(e => e.AssetId == assetId)
            .OrderBy(e => e.ScheduleDate)
            .ToListAsync(cancellationToken);

    public async Task AddCategoryAsync(AssetCategory category, CancellationToken cancellationToken = default)
    {
        await _dbContext.AssetCategories.AddAsync(category, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddAssetAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        await _dbContext.Assets.AddAsync(asset, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddScheduleRangeAsync(
        IReadOnlyList<AssetDepreciationSchedule> lines,
        CancellationToken cancellationToken = default)
    {
        _dbContext.AssetDepreciationSchedules.AddRange(lines);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddGlEntriesAsync(IReadOnlyList<GLEntry> glEntries, CancellationToken cancellationToken = default)
    {
        _dbContext.GLEntries.AddRange(glEntries);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAssetAsync(Asset asset, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Spec AS-06: another operation transitioned the asset between our load and this save -
            // the RowVersion WHERE clause matched 0 rows. Translate into the typed domain failure
            // the handlers convert into a 409 Result.Failure, mirroring the manufacturing repository.
            throw new ConcurrencyConflictException(nameof(Asset), asset.Id, ex);
        }
    }

    public async Task UpdateScheduleAsync(AssetDepreciationSchedule line, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(nameof(AssetDepreciationSchedule), line.Id, ex);
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
