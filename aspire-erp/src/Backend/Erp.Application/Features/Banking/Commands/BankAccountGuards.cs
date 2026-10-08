using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;

namespace Erp.Application.Features.Banking.Commands;

/// <summary>
/// Shared guards for the bank account master (create/update): pure field rules plus the
/// GL-account lookup that needs data access.
/// </summary>
public static class BankAccountGuards
{
    public const int MaxNameLength = 100;
    public const int MaxNumberLength = 50;

    public static void EnsureValidFields(
        Guid companyId,
        string? accountName,
        string? bankName,
        string? accountNumber)
    {
        if (companyId == Guid.Empty)
        {
            throw new BankingValidationException(
                BankingErrorCodes.CompanyNotFound,
                "A bank account must belong to a company (CompanyId is required).");
        }

        if (string.IsNullOrWhiteSpace(accountName))
        {
            throw new BankingValidationException(
                BankingErrorCodes.BankAccountNameRequired,
                "Account Name is required.");
        }

        if (accountName.Trim().Length > MaxNameLength)
        {
            throw new BankingValidationException(
                BankingErrorCodes.BankAccountNameRequired,
                $"Account Name must not exceed {MaxNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(bankName))
        {
            throw new BankingValidationException(
                BankingErrorCodes.BankNameRequired,
                "Bank Name is required.");
        }

        if (bankName.Trim().Length > MaxNameLength)
        {
            throw new BankingValidationException(
                BankingErrorCodes.BankNameRequired,
                $"Bank Name must not exceed {MaxNameLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(accountNumber))
        {
            throw new BankingValidationException(
                BankingErrorCodes.BankAccountNumberRequired,
                "Account Number is required.");
        }

        if (accountNumber.Trim().Length > MaxNumberLength)
        {
            throw new BankingValidationException(
                BankingErrorCodes.BankAccountNumberRequired,
                $"Account Number must not exceed {MaxNumberLength} characters.");
        }
    }

    /// <summary>
    /// The linked GL account must exist in this tenant, belong to the same company, be active
    /// and be a leaf (posting) account - the Constitution III.3 leaf-posting guard applied to
    /// the bank account master itself.
    /// </summary>
    public static async Task RequirePostableGlAccountAsync(
        IAccountRepository accounts,
        Guid glAccountId,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var account = await accounts.GetByIdAsync(glAccountId, cancellationToken);

        if (account is null || account.CompanyId != companyId || !account.IsActive || account.IsGroup)
        {
            throw new BankingValidationException(
                BankingErrorCodes.InvalidGlAccount,
                $"GL account '{glAccountId}' is not a postable (active leaf) account of this company.");
        }
    }
}
