using Erp.Domain.Exceptions;

namespace Erp.Domain.Entities;

/// <summary>
/// Pure C# validation for the Account aggregate (Task 2.1). No EF Core, no NuGet packages -
/// Constitution Article I.2 keeps Erp.Domain dependency-free, so every rule here is unit-tested
/// from <c>tests/Erp.Domain.UnitTests</c> without a database.
/// </summary>
/// <remarks>
/// Rules come from .specify/domain_business_rules_ddd.md 3.2 (invariants) and 1 (ubiquitous
/// language): a Leaf / Posting Account is terminal, a child inherits its parent's RootType, and a
/// Group Account is the only valid parent.
/// </remarks>
public static class AccountValidator
{
    public const int MaxAccountCodeLength = 50;
    public const int MaxAccountNameLength = 150;
    public const int MaxCurrencyLength = 3;

    /// <summary>Field-level rules: required code/name (50/150), defined RootType, currency, company.</summary>
    /// <param name="type">
    /// Optional ERPNext account_type (Task 1.1): defaults to <see cref="AccountType.Other"/> so
    /// existing callers that predate the Type column keep compiling; CreateAccountCommandHandler
    /// always passes the resolved value (explicit client value or per-RootType default).
    /// </param>
    /// <exception cref="AccountValidationException">An invariant was violated.</exception>
    public static void EnsureValidFields(
        Guid companyId,
        string? accountCode,
        string? accountName,
        AccountRootType rootType,
        string? currency,
        AccountType type = AccountType.Other)
    {
        if (companyId == Guid.Empty)
        {
            throw new AccountValidationException(
                AccountErrorCodes.CompanyRequired,
                "An account must belong to a company (CompanyId is required).");
        }

        if (string.IsNullOrWhiteSpace(accountCode))
        {
            throw new AccountValidationException(
                AccountErrorCodes.AccountCodeRequired,
                "AccountCode is required.");
        }

        if (accountCode.Length > MaxAccountCodeLength)
        {
            throw new AccountValidationException(
                AccountErrorCodes.AccountCodeTooLong,
                $"AccountCode must not exceed {MaxAccountCodeLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(accountName))
        {
            throw new AccountValidationException(
                AccountErrorCodes.AccountNameRequired,
                "AccountName is required.");
        }

        if (accountName.Length > MaxAccountNameLength)
        {
            throw new AccountValidationException(
                AccountErrorCodes.AccountNameTooLong,
                $"AccountName must not exceed {MaxAccountNameLength} characters.");
        }

        if (!Enum.IsDefined(rootType))
        {
            throw new AccountValidationException(
                AccountErrorCodes.InvalidRootType,
                $"RootType must be one of: {string.Join(", ", Enum.GetNames<AccountRootType>())}.");
        }

        if (!Enum.IsDefined(type))
        {
            // Task 1.1: Type is persisted as a NAME string (NVARCHAR(50)); an out-of-range cast
            // (e.g. a JSON number outside the enum) must fail as a domain rule, not round-trip an
            // empty/invalid value into the column.
            throw new AccountValidationException(
                AccountErrorCodes.InvalidAccountType,
                $"Type must be one of: {string.Join(", ", Enum.GetNames<AccountType>())}.");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Length > MaxCurrencyLength)
        {
            throw new AccountValidationException(
                AccountErrorCodes.CurrencyInvalid,
                $"Currency must be a non-empty ISO 4217 code of at most {MaxCurrencyLength} characters.");
        }
    }

    /// <summary>
    /// Parent rules for a child account (<paramref name="candidate"/>): the parent must exist in the
    /// same company, must be a Group Account (a leaf is terminal), must not be the account itself,
    /// and the child must inherit the parent's RootType (business rule 3.2.2).
    /// </summary>
    /// <exception cref="AccountValidationException">An invariant was violated.</exception>
    public static void EnsureValidParent(Account candidate, Account parent)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(parent);

        if (parent.Id == candidate.Id || candidate.ParentAccountId == candidate.Id)
        {
            throw new AccountValidationException(
                AccountErrorCodes.ParentIsSelf,
                "An account cannot be its own parent.");
        }

        if (parent.CompanyId != candidate.CompanyId)
        {
            throw new AccountValidationException(
                AccountErrorCodes.ParentNotInSameCompany,
                "The parent account must belong to the same company.");
        }

        if (!parent.IsGroup)
        {
            // Ubiquitous language 1: a Leaf / Posting Account is terminal - only Group Accounts
            // (non-posting folders) may have children.
            throw new AccountValidationException(
                AccountErrorCodes.ParentIsNotGroup,
                $"Account '{parent.AccountCode}' is a leaf (posting) account and cannot have children.");
        }

        if (candidate.RootType != parent.RootType)
        {
            // Invariant 3.2.2: Root Type Inheritance - a child must inherit the parent's RootType.
            throw new AccountValidationException(
                AccountErrorCodes.RootTypeMismatch,
                $"A child account must inherit the parent's RootType ('{parent.RootType}') but was '{candidate.RootType}'.");
        }
    }

    /// <summary>
    /// Cycle prevention for the parent chain, from the proposed parent up to the root. Needs the
    /// loaded ancestor graph, so the command handler walks it through IAccountRepository first and
    /// passes the ids here (decision C5). Rejects both the candidate appearing in its own ancestor
    /// chain and a chain that already loops (persisted data corruption - the walk would never end).
    /// </summary>
    /// <exception cref="AccountValidationException">A cycle was detected.</exception>
    public static void EnsureNoCycle(Guid candidateId, IReadOnlyList<Guid> ancestorIdsFromParentToRoot)
    {
        ArgumentNullException.ThrowIfNull(ancestorIdsFromParentToRoot);

        if (ancestorIdsFromParentToRoot.Contains(candidateId))
        {
            throw new AccountValidationException(
                AccountErrorCodes.CycleDetected,
                "Cycle detected: the parent chain contains the account itself (the account would be its own ancestor).");
        }

        var seen = new HashSet<Guid>();
        foreach (var id in ancestorIdsFromParentToRoot)
        {
            if (!seen.Add(id))
            {
                throw new AccountValidationException(
                    AccountErrorCodes.CycleDetected,
                    "Cycle detected: the stored parent chain loops back on itself.");
            }
        }
    }
}
