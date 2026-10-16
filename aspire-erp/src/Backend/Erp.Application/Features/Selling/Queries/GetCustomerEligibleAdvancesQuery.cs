using Erp.Application.Common;
using Erp.Application.DTOs;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Selling.Queries;

/// <summary>
/// Advance funding source for the invoice form (sales-invoice ERPNext parity): submitted
/// customer receipts with a remaining unallocated balance, oldest payment first.
/// </summary>
public sealed record GetCustomerEligibleAdvancesQuery(
    Guid CompanyId,
    Guid CustomerId) : IQuery<IReadOnlyList<EligibleAdvanceDto>>;

public sealed class GetCustomerEligibleAdvancesQueryHandler
    : IQueryHandler<GetCustomerEligibleAdvancesQuery, IReadOnlyList<EligibleAdvanceDto>>
{
    private readonly IBankRepository _banks;

    public GetCustomerEligibleAdvancesQueryHandler(IBankRepository banks)
    {
        _banks = banks;
    }

    public async Task<IReadOnlyList<EligibleAdvanceDto>> HandleAsync(
        GetCustomerEligibleAdvancesQuery query,
        CancellationToken cancellationToken = default)
    {
        var advances = await _banks.GetEligibleAdvancesAsync(
            query.CompanyId, query.CustomerId, cancellationToken);

        return advances
            .Select(p => new EligibleAdvanceDto(
                p.Id,
                p.VoucherNo,
                p.PaymentDate,
                p.PaidAmount,
                p.UnallocatedAmount))
            .ToList();
    }
}
