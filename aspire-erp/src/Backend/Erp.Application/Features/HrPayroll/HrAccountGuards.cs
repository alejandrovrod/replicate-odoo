using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.HrPayroll;

/// <summary>
/// Shared Constitution III.3 guard for the HR pipelines (Task 12.2): every GL-referenced
/// account must exist, be active, be a leaf (posting) account and belong to the company whose
/// books are written. Mirrors <c>AssetAccountGuards</c> line-for-line, but raises
/// <see cref="HrValidationException"/> with <c>HrPayrollErrorCodes.InvalidGlAccount</c> so the
/// HR handlers report HR-vocabulary failures (a shared guard would leak asset-domain codes).
/// </summary>
internal static class HrAccountGuards
{
    internal static async Task<Account> RequirePostableAccountAsync(
        IAccountRepository accounts,
        Guid accountId,
        Guid companyId,
        string ownerContext,
        CancellationToken cancellationToken)
    {
        var account = await accounts.GetByIdAsync(accountId, cancellationToken)
            ?? throw new HrValidationException(
                HrPayrollErrorCodes.InvalidGlAccount,
                $"The account '{accountId}' linked to {ownerContext} does not exist in this tenant.");

        if (!account.IsActive)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' ({ownerContext}) is inactive and cannot receive General Ledger postings.");
        }

        if (account.IsGroup)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' ({ownerContext}) is a group account and cannot receive General Ledger postings.");
        }

        if (account.CompanyId != companyId)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidGlAccount,
                $"Account '{account.AccountCode}' ({ownerContext}) belongs to another company and cannot be posted from this company.");
        }

        return account;
    }
}
