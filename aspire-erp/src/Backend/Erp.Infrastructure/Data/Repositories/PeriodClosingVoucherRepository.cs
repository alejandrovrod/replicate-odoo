using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Infrastructure.Data.Repositories;

public class PeriodClosingVoucherRepository : IPeriodClosingVoucherRepository
{
    private readonly AppDbContext _context;

    public PeriodClosingVoucherRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<PeriodClosingVoucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return _context.PeriodClosingVouchers.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task AddAsync(PeriodClosingVoucher voucher, CancellationToken cancellationToken = default)
    {
        await _context.PeriodClosingVouchers.AddAsync(voucher, cancellationToken);
    }

    public void Update(PeriodClosingVoucher voucher)
    {
        _context.PeriodClosingVouchers.Update(voucher);
    }

    public Task<List<PeriodClosingVoucher>> GetPagedAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default)
    {
        return _context.PeriodClosingVouchers
            .Where(x => x.CompanyId == companyId)
            .OrderByDescending(x => x.PostingDate)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
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

    public Task<List<GLEntry>> GetUnclosedPLEntriesAsync(Guid companyId, DateOnly postingDate, CancellationToken cancellationToken = default)
    {
        return _context.GLEntries
            .Include(x => x.Account)
            .Where(x => x.CompanyId == companyId 
                     && x.PostingDate <= postingDate
                     && !x.IsCancelled
                     && (x.Account.RootType == Erp.Domain.Entities.AccountRootType.Income || x.Account.RootType == Erp.Domain.Entities.AccountRootType.Expense))
            .ToListAsync(cancellationToken);
    }

    public async Task AddGLEntriesAsync(IEnumerable<GLEntry> entries, CancellationToken cancellationToken = default)
    {
        await _context.GLEntries.AddRangeAsync(entries, cancellationToken);
    }

    public Task<List<GLEntry>> GetGLEntriesByVoucherAsync(string voucherNo, CancellationToken cancellationToken = default)
    {
        return _context.GLEntries
            .Where(x => x.VoucherType == "PeriodClosingVoucher" && x.VoucherNo == voucherNo && !x.IsCancelled)
            .ToListAsync(cancellationToken);
    }

    public void UpdateGLEntries(IEnumerable<GLEntry> entries)
    {
        _context.GLEntries.UpdateRange(entries);
    }
}
