using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Assets;

/// <summary>
/// Shared Constitution III.3 guard for the asset pipelines (Tasks 10.1-10.2): every
/// GL-referenced account must exist, be active, be a leaf (posting) account and belong to the
/// company whose books are written. Same shape as the stock/manufacturing
/// <c>RequirePostableAccountAsync</c> posting-service guards; shared here so the category and
/// capitalization handlers enforce identical rules.
/// </summary>
internal static class AssetAccountGuards
{
    internal static async Task<Account> RequirePostableAccountAsync(
        IAccountRepository accounts,
        Guid accountId,
        Guid companyId,
        string ownerContext,
        CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new AssetValidationException(
                AssetErrorCodes.InvalidGlAccount,
                $"The account '{accountId}' linked to {ownerContext} does not exist in this tenant.");

        if (!account.IsActive)
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' ({ownerContext}) is inactive and cannot receive General Ledger postings.");
        }

        if (account.IsGroup)
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' ({ownerContext}) is a group account and cannot receive General Ledger postings.");
        }

        if (account.CompanyId != companyId)
        {
            throw new AssetValidationException(
                AssetErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' ({ownerContext}) belongs to another company and cannot be posted from this company.");
        }

        return account;
    }
}
