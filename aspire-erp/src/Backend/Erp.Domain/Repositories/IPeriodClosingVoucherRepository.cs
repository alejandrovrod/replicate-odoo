using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Erp.Domain.Entities;

namespace Erp.Domain.Repositories;

public interface IPeriodClosingVoucherRepository
{
    Task<PeriodClosingVoucher?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(PeriodClosingVoucher voucher, CancellationToken cancellationToken = default);
    void Update(PeriodClosingVoucher voucher);
    Task<List<PeriodClosingVoucher>> GetPagedAsync(Guid companyId, int skip, int take, CancellationToken cancellationToken = default);
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default);
    Task<List<GLEntry>> GetUnclosedPLEntriesAsync(Guid companyId, DateOnly postingDate, CancellationToken cancellationToken = default);
    Task AddGLEntriesAsync(IEnumerable<GLEntry> entries, CancellationToken cancellationToken = default);
    Task<List<GLEntry>> GetGLEntriesByVoucherAsync(string voucherNo, CancellationToken cancellationToken = default);
    void UpdateGLEntries(IEnumerable<GLEntry> entries);
}
