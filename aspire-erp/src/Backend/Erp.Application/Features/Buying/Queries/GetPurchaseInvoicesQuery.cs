using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Loads the most recent purchase invoices of one company with their lines - the vendor-bill
/// audit view behind Task 4.3. Defaults to 50.
/// </summary>
public sealed record GetPurchaseInvoicesQuery(Guid CompanyId, int Limit = 50)
    : IQuery<IReadOnlyList<PurchaseInvoiceDto>>;
