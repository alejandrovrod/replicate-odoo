using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Buying.Commands;

/// <summary>
/// Shared validation for the per-company account list of a Supplier (module buying-suppliers,
/// spec §1 gap D2): unique company per entry, and - when the repositories are wired - each company,
/// payable account and advance account must exist in the tenant.
/// </summary>
internal static class SupplierCompanyAccountRules
{
    public static async Task ValidateAsync(
        IReadOnlyList<SupplierCompanyAccountInput>? accounts,
        ICompanyRepository? companies,
        IAccountRepository? accountRepository,
        CancellationToken cancellationToken)
    {
        if (accounts is null || accounts.Count == 0)
        {
            return;
        }

        var seenCompanies = new HashSet<Guid>();
        foreach (var account in accounts)
        {
            if (account.CompanyId == Guid.Empty)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.CompanyNotFound,
                    "A company account entry references an empty CompanyId.");
            }

            if (!seenCompanies.Add(account.CompanyId))
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.InvalidGlAccount,
                    $"Company '{account.CompanyId}' is configured more than once.");
            }

            if (companies is not null
                && await companies.GetByIdAsync(account.CompanyId, cancellationToken) is null)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.CompanyNotFound,
                    $"Company '{account.CompanyId}' was not found in this tenant.");
            }

            if (accountRepository is null)
            {
                continue;
            }

            if (await accountRepository.GetByIdAsync(account.PayableAccountId, cancellationToken) is null)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.InvalidGlAccount,
                    $"Payable account '{account.PayableAccountId}' was not found in this tenant.");
            }

            if (account.AdvanceAccountId.HasValue
                && await accountRepository.GetByIdAsync(account.AdvanceAccountId.Value, cancellationToken) is null)
            {
                throw new PurchaseValidationException(
                    PurchaseErrorCodes.InvalidGlAccount,
                    $"Advance account '{account.AdvanceAccountId.Value}' was not found in this tenant.");
            }
        }
    }
}
