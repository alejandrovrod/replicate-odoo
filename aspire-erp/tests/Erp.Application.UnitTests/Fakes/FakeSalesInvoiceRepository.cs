using Erp.Domain.Common;
using Erp.Domain.Entities;
using Erp.Domain.Repositories;

namespace Erp.Application.UnitTests.Fakes;

/// <summary>
/// In-memory <see cref="ISalesInvoiceRepository"/> (spec R-12): tests seed open invoices and
/// assert on the outstanding/paid/status mutations the payment handlers perform.
/// </summary>
public sealed class FakeSalesInvoiceRepository : ISalesInvoiceRepository
{
    private readonly List<SalesInvoice> _invoices = new();

    /// <summary>Invoices visible to the handler under test.</summary>
    public IReadOnlyList<SalesInvoice> Invoices => _invoices;

    public void Seed(params SalesInvoice[] invoices) => _invoices.AddRange(invoices);

    public Task<string> GenerateNextInvoiceNumberAsync(CancellationToken cancellationToken = default)
        => Task.FromResult("SINV-2026-00001");

    public Task AddAsync(SalesInvoice invoice, CancellationToken cancellationToken = default)
    {
        _invoices.Add(invoice);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(SalesInvoice invoice, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<SalesInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_invoices.FirstOrDefault(i => i.Id == id));

    public Task<PagedResult<SalesInvoice>> GetRecentByCompanyAsync(
        Guid companyId, PagedRequest paging, CancellationToken cancellationToken = default)
    {
        var filtered = _invoices
            .Where(i => i.CompanyId == companyId)
            .OrderByDescending(i => i.CreatedAt)
            .ThenByDescending(i => i.Id)
            .ToList();
        var total = filtered.Count;
        var items = filtered.Skip((paging.PageNumber - 1) * paging.PageSize).Take(paging.PageSize).ToList();
        return Task.FromResult(new PagedResult<SalesInvoice>(items, total, paging.PageNumber, paging.PageSize));
    }

    public Task AddGlEntriesAsync(IReadOnlyList<GLEntry> entries, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default)
        => operation(cancellationToken);

    public Task<IReadOnlyList<SalesInvoice>> GetOutstandingByCustomerAsync(
        Guid companyId, Guid customerId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<SalesInvoice>>(
            _invoices
                .Where(i => i.CompanyId == companyId
                    && i.CustomerId == customerId
                    && i.OutstandingAmount > 0m
                    && (i.Status == SalesInvoiceStatus.Unpaid || i.Status == SalesInvoiceStatus.PartiallyPaid))
                .OrderBy(i => i.DueDate)
                .ThenBy(i => i.Id)
                .ToList());
}
