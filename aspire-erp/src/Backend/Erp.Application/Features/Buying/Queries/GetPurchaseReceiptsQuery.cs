using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Buying.Queries;

/// <summary>
/// Loads the most recent purchase receipts of one company with their lines (including the line
/// ids invoice lines bill against) - the audit view behind Task 4.2. Defaults to 50.
/// </summary>
public sealed record GetPurchaseReceiptsQuery(Guid CompanyId, int Limit = 50)
    : IQuery<IReadOnlyList<PurchaseReceiptDto>>;
