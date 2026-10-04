using System;
using System.Threading;
using System.Threading.Tasks;
using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

public interface ISalesInvoiceRepository
{
    Task<string> GenerateNextInvoiceNumberAsync(CancellationToken cancellationToken = default);
    Task AddAsync(SalesInvoice invoice, CancellationToken cancellationToken = default);
    Task UpdateAsync(SalesInvoice invoice, CancellationToken cancellationToken = default);
    Task<SalesInvoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddGlEntriesAsync(IReadOnlyList<GLEntry> entries, CancellationToken cancellationToken = default);
    Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default);
}
