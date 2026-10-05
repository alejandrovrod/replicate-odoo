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

    /// <summary>
    /// Decision D3 (Tasks 12.3-12.4, same shape as the manufacture absorption leg): company-level
    /// GL defaults are stored as account CODES and resolve here to exactly one active leaf
    /// account of the company (missing = configuration failure, ambiguous = configuration
    /// failure - both loud, never silent).
    /// </summary>
    internal static async Task<Account> RequireAccountByCodeAsync(
        IAccountRepository accounts,
        Guid companyId,
        string? accountCode,
        string settingName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(accountCode))
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidGlAccount,
                $"Company '{companyId}' does not configure {settingName}. "
                + "Seed it with an active leaf account code (e.g. PayrollPayableAccountCode = '2150').");
        }

        var matches = await accounts.FindActiveLeafByCodeAsync(companyId, accountCode, cancellationToken);

        if (matches.Count == 0)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidGlAccount,
                $"{settingName} = '{accountCode}' does not resolve to an ACTIVE LEAF account of company "
                + $"'{companyId}'. Seed the account (IsActive = 1, IsGroup = 0) or fix the company setting.");
        }

        if (matches.Count > 1)
        {
            throw new HrValidationException(
                HrPayrollErrorCodes.InvalidGlAccount,
                $"{settingName} = '{accountCode}' is ambiguous: {matches.Count} active leaf accounts share that "
                + $"code in company '{companyId}'. Account codes must be unique per company.");
        }

        return matches[0];
    }
}
