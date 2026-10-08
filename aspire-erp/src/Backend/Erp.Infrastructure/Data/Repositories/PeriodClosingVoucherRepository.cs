using System.Data;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

/// <summary>
/// EF implementation of <see cref="IPeriodClosingVoucherRepository"/> (R-13 rewrite, spec §7).
/// The submit/preview balance query is FY-windowed (plan.md §4); cancellation is reversal-append
/// only — there is intentionally NO update path for <see cref="GLEntry"/> rows (append-only,
/// Constitution III.2).
/// </summary>
public class PeriodClosingVoucherRepository : IPeriodClosingVoucherRepository
{
    private const string ClosingVoucherType = "PeriodClosingVoucher";

    private readonly AppDbContext _context;

    public PeriodClosingVoucherRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<PeriodClosingVoucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.PeriodClosingVouchers
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public Task<PeriodClosingVoucher?> GetByIdempotencyKeyAsync(Guid companyId, string idempotencyKey, CancellationToken cancellationToken = default) =>
        _context.PeriodClosingVouchers
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(v => v.CompanyId == companyId && v.IdempotencyKey == idempotencyKey, cancellationToken);

    public async Task AddAsync(PeriodClosingVoucher voucher, CancellationToken cancellationToken = default) =>
        await _context.PeriodClosingVouchers.AddAsync(voucher, cancellationToken);

    public void Update(PeriodClosingVoucher voucher) => _context.PeriodClosingVouchers.Update(voucher);

    public Task<List<PeriodClosingVoucher>> GetPagedAsync(Guid companyId, Guid? fiscalYearId, DocumentStatus? status, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = _context.PeriodClosingVouchers.Where(x => x.CompanyId == companyId);
        if (fiscalYearId.HasValue)
        {
            query = query.Where(x => x.FiscalYearId == fiscalYearId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.DocumentStatus == status.Value);
        }

        return query.OrderByDescending(x => x.PostingDate).Skip(skip).Take(take).ToListAsync(cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return await operation();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var result = await operation();
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<T> ExecuteInSerializableTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
    {
        if (_context.Database.CurrentTransaction != null)
        {
            return await operation();
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var result = await operation();
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<List<UnclosedPLBalance>> GetUnclosedPLBalancesAsync(Guid companyId, Guid fiscalYearId, CancellationToken cancellationToken = default)
    {
        var year = await _context.FiscalYears
            .FirstOrDefaultAsync(x => x.Id == fiscalYearId && x.CompanyId == companyId, cancellationToken)
            ?? throw new InvalidOperationException($"Fiscal year '{fiscalYearId}' was not found for this company.");

        var closedVoucherNos = await _context.PeriodClosingVouchers
            .Where(v => v.CompanyId == companyId
                && v.FiscalYearId == fiscalYearId
                && v.DocumentStatus == DocumentStatus.Submitted)
            .Select(v => v.VoucherNo)
            .ToListAsync(cancellationToken);

        var rows = await (
            from gl in _context.GLEntries
            join a in _context.Accounts on gl.AccountId equals a.Id
            where gl.CompanyId == companyId
                && gl.PostingDate >= year.StartDate
                && gl.PostingDate <= year.EndDate
                && !gl.IsCancelled
                && !(gl.VoucherType == ClosingVoucherType && closedVoucherNos.Contains(gl.VoucherNo))
                && (a.RootType == AccountRootType.Income || a.RootType == AccountRootType.Expense)
                && !a.IsGroup
                && a.IsActive
                && a.CompanyId == companyId
            group new { gl, a } by new { gl.AccountId, a.AccountCode, a.AccountName, a.RootType } into g
            let balance = g.Sum(x => x.gl.Credit - x.gl.Debit)
            where balance != 0
            select new UnclosedPLBalance(
                g.Key.AccountId,
                g.Key.AccountCode,
                g.Key.AccountName,
                g.Key.RootType,
                balance))
            .ToListAsync(cancellationToken);

        return rows;
    }

    public Task<bool> HasSubmittedCloseAsync(Guid companyId, Guid fiscalYearId, Guid? excludeVoucherId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.PeriodClosingVouchers
            .Where(v => v.CompanyId == companyId
                && v.FiscalYearId == fiscalYearId
                && v.DocumentStatus == DocumentStatus.Submitted);

        if (excludeVoucherId.HasValue)
        {
            query = query.Where(v => v.Id != excludeVoucherId.Value);
        }

        return query.AnyAsync(cancellationToken);
    }

    public Task<bool> HasDraftVoucherAsync(Guid fiscalYearId, CancellationToken cancellationToken = default) =>
        _context.PeriodClosingVouchers
            .AnyAsync(v => v.FiscalYearId == fiscalYearId && v.DocumentStatus == DocumentStatus.Draft, cancellationToken);

    public Task<List<string>> GetVoucherNosOfYearAsync(Guid fiscalYearId, CancellationToken cancellationToken = default) =>
        _context.PeriodClosingVouchers
            .Where(v => v.FiscalYearId == fiscalYearId && v.DocumentStatus == DocumentStatus.Submitted)
            .Select(v => v.VoucherNo)
            .ToListAsync(cancellationToken);

    public async Task<string> NextClosingVoucherNumberAsync(Guid companyId, FiscalYear fiscalYear, CancellationToken cancellationToken = default)
    {
        var prefix = $"PCV-{fiscalYear.StartDate.Year}-";
        var maxNo = await _context.PeriodClosingVouchers
            .Where(v => v.CompanyId == companyId && v.FiscalYearId == fiscalYear.Id && v.VoucherNo.StartsWith(prefix))
            .OrderByDescending(v => v.VoucherNo)
            .Select(v => v.VoucherNo)
            .FirstOrDefaultAsync(cancellationToken);

        var seq = 1;
        if (maxNo is not null && maxNo.Length > prefix.Length && int.TryParse(maxNo[(prefix.Length)..], out var last))
        {
            seq = last + 1;
        }

        return $"{prefix}{seq:D5}";
    }

    public async Task AddGLEntriesAsync(IEnumerable<GLEntry> entries, CancellationToken cancellationToken = default) =>
        await _context.GLEntries.AddRangeAsync(entries, cancellationToken);

    public Task<List<GLEntry>> GetGLEntriesByVoucherAsync(Guid voucherId, CancellationToken cancellationToken = default) =>
        _context.GLEntries
            .Where(x => x.VoucherType == ClosingVoucherType && x.VoucherId == voucherId)
            .OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

    public async Task AddLinesAsync(IEnumerable<PeriodClosingVoucherLine> lines, CancellationToken cancellationToken = default) =>
        await _context.PeriodClosingVoucherLines.AddRangeAsync(lines, cancellationToken);
}
