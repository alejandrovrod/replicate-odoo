using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Manufacturing.Commands;

/// <summary>
/// Moves a work order's components Stores -&gt; WIP (Task 9.4, spec MF-02): every BOM line scaled
/// by the authorized quantity posts as one <c>MaterialTransfer</c> voucher through the stock
/// engine, which debits the WIP warehouse account and credits the Stores account at FIFO value
/// (Dr 1320 / Cr 1310). Covers the FULL authorized quantity; partial staging stays deferred.
/// On success the order advances Submitted -&gt; InProcess.
/// </summary>
public sealed record TransferMaterialsToWipCommand(
    Guid CompanyId,
    Guid WorkOrderId,
    DateOnly? PostingDate = null) : ICommand<Result<StockEntryPostingDto>>;
