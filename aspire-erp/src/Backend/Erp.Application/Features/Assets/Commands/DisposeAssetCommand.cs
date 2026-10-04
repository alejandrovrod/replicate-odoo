using Erp.Application.Common;
using Erp.Application.DTOs;

namespace Erp.Application.Features.Assets.Commands;

/// <summary>
/// Disposes an asset by sale or scrap (Task 10.5, scenarios AS-03/AS-05): clears the gross cost
/// and the accrued depreciation from the books and books the variance to the category's
/// gain/loss account - all inside ONE transaction with a gapless voucher.
/// </summary>
/// <remarks>
/// <para>
/// Cash-via-bank sales and $0 scraps ONLY: <see cref="ProceedsAmount"/> &gt; 0 MUST travel with
/// a company-owned <see cref="ProceedsBankAccountId"/> (resolved to its postable GL account),
/// and $0 MUST travel with null. Credit sales (proceeds via Accounts Receivable) are out of
/// scope - no AR path exists; a sale on credit stays a follow-up, not a silent half-booking.
/// </para>
/// <para>Assets enter the books via SQL seeds (like BOMs); no asset-create command exists.</para>
/// </remarks>
public sealed record DisposeAssetCommand(
    Guid CompanyId,
    Guid AssetId,
    DateOnly DisposalDate,
    decimal ProceedsAmount,
    Guid? ProceedsBankAccountId = null) : ICommand<Result<AssetDisposalDto>>;
