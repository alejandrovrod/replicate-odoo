using System;
using System.Threading;
using System.Threading.Tasks;
using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Infrastructure.Data.Pagination;
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
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Spec R-12 PE-07: a concurrent payment moved this invoice between our load and this
            // save - the RowVersion WHERE clause matched 0 rows. Typed like every other master
            // repository so handlers map it to a 409 instead of leaking EF.
            _context.Entry(invoice).State = EntityState.Detached;
            throw new ConcurrencyConflictException(nameof(SalesInvoice), invoice.Id, ex);
        }
    }

    public async Task<IReadOnlyList<SalesInvoice>> GetOutstandingByCustomerAsync(
        Guid companyId,
        Guid customerId,
        CancellationToken cancellationToken = default)
        => await _context.SalesInvoices
            .Where(i => i.CompanyId == companyId
                && i.CustomerId == customerId
                && i.OutstandingAmount > 0m
                && (i.Status == SalesInvoiceStatus.Unpaid || i.Status == SalesInvoiceStatus.PartiallyPaid))
            .OrderBy(i => i.DueDate)
            .ThenBy(i => i.Id)
            .ToListAsync(cancellationToken);

    public async Task<SalesInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.SalesInvoices
            .Include(x => x.Items)
            .Include(x => x.Customer)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task<PagedResult<SalesInvoice>> GetRecentByCompanyAsync(
        Guid companyId,
        PagedRequest paging,
        CancellationToken cancellationToken = default)
        => await _context.SalesInvoices
            .Where(i => i.CompanyId == companyId)
            .Include(i => i.Items)
            .Include(i => i.Customer)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .ToPagedResultAsync(paging, cancellationToken);

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
