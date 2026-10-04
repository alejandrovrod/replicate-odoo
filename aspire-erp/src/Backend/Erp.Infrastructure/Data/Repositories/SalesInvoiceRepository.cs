using System;
using System.Threading;
using System.Threading.Tasks;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data.Repositories;

public sealed class SalesInvoiceRepository : ISalesInvoiceRepository
{
    private readonly AppDbContext _context;

    public SalesInvoiceRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<string> GenerateNextInvoiceNumberAsync(CancellationToken cancellationToken = default)
    {
        var count = await _context.SalesInvoices.IgnoreQueryFilters().CountAsync(cancellationToken);
        return $"SINV-{DateTime.UtcNow.Year}-{count + 1:D5}";
    }

    public async Task AddAsync(SalesInvoice invoice, CancellationToken cancellationToken = default)
    {
        await _context.SalesInvoices.AddAsync(invoice, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SalesInvoice invoice, CancellationToken cancellationToken = default)
    {
        _context.SalesInvoices.Update(invoice);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<SalesInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SalesInvoices
            .Include(x => x.Items)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddGlEntriesAsync(IReadOnlyList<GLEntry> entries, CancellationToken cancellationToken = default)
    {
        await _context.GLEntries.AddRangeAsync(entries, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var result = await operation(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }
}
