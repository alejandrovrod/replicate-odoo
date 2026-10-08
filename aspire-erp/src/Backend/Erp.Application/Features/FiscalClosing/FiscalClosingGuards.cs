using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Erp.Domain.Repositories;
using Erp.Domain.Services;
using Erp.Application.DTOs;

namespace Erp.Application.Features.FiscalClosing;

/// <summary>
/// Generic validation failure of the Fiscal Closing module: carries any
/// <see cref="FiscalClosingErrorCodes"/> code that has no dedicated typed exception
/// (not-found, bad-request shapes). Thrown by handlers, caught into <c>Result.Failure</c>.
/// </summary>
public sealed class FiscalClosingValidationException : FiscalClosingException
{
    public FiscalClosingValidationException(string code, string message)
        : base(code, message)
    {
    }
}

/// <summary>
/// Shared guards of the Fiscal Closing CQRS pipeline (plan.md §4 validation order).
/// </summary>
public static class FiscalClosingGuards
{
    /// <summary>
    /// Compare-and-swap on the client-supplied token: null token = no client check (the
    /// server-side rowversion still guards the load/save race); present but stale = 409.
    /// </summary>
    /// <exception cref="ConcurrencyConflictException">Stale token.</exception>
    public static void EnsureRowVersion(byte[] current, byte[]? expected, string entityName, Guid entityId)
    {
        if (expected is null)
        {
            return;
        }

        if (!current.SequenceEqual(expected))
        {
            throw new ConcurrencyConflictException(entityName, entityId);
        }
    }

    /// <summary>
    /// Spec FC-03: the retained account MUST exist and satisfy ALL of: same tenant (guaranteed by
    /// the global query filter — a cross-tenant id reads as missing), same company,
    /// <c>RootType = Equity</c>, non-group, active. Otherwise
    /// <c>invalid_retained_earnings_account</c> (400-class).
    /// </summary>
    /// <exception cref="InvalidRetainedEarningsException">Any gate fails.</exception>
    public static async Task<Account> EnsureValidRetainedEarningsAsync(
        IAccountRepository accounts,
        Guid companyId,
        Guid retainedAccountId,
        CancellationToken cancellationToken = default)
    {
        var account = await accounts.GetByIdAsync(retainedAccountId, cancellationToken);
        if (account is null)
        {
            throw new InvalidRetainedEarningsException(retainedAccountId, "the account does not exist in this tenant");
        }

        if (account.CompanyId != companyId)
        {
            throw new InvalidRetainedEarningsException(retainedAccountId, "the account belongs to another company");
        }

        if (account.RootType != AccountRootType.Equity)
        {
            throw new InvalidRetainedEarningsException(
                retainedAccountId, $"RootType is '{account.RootType}', not Equity");
        }

        if (account.IsGroup)
        {
            throw new InvalidRetainedEarningsException(retainedAccountId, "group accounts never receive postings");
        }

        if (!account.IsActive)
        {
            throw new InvalidRetainedEarningsException(retainedAccountId, "the account is inactive");
        }

        return account;
    }

    /// <summary>
    /// Resolves the effective retained account: the voucher's explicit id wins; when empty, the
    /// company default is used; when both missing → <c>invalid_retained_earnings_account</c>.
    /// </summary>
    public static async Task<Account> ResolveRetainedEarningsAsync(
        IAccountRepository accounts,
        Company company,
        Guid explicitRetainedAccountId,
        CancellationToken cancellationToken = default)
    {
        if (explicitRetainedAccountId != Guid.Empty)
        {
            return await EnsureValidRetainedEarningsAsync(accounts, company.Id, explicitRetainedAccountId, cancellationToken);
        }

        if (company.DefaultRetainedEarningsAccountId.HasValue)
        {
            return await EnsureValidRetainedEarningsAsync(
                accounts, company.Id, company.DefaultRetainedEarningsAccountId.Value, cancellationToken);
        }

        throw new InvalidRetainedEarningsException(
            Guid.Empty,
            "no retained earnings account was supplied and the company has no default configured");
    }

    /// <summary>Builds the read-only preview envelope from live balances (plan.md §4).</summary>
    public static ClosingPreviewDto BuildPreview(
        Guid companyId,
        Guid fiscalYearId,
        IReadOnlyList<UnclosedPLBalance> balances,
        Guid retainedAccountId,
        string retainedCode,
        string retainedName)
    {
        var inputs = balances
            .Select(b => new ClosingBalanceInput(b.AccountId, b.RootType, b.Balance))
            .ToList();

        var computation = PeriodClosingCalculator.Compute(inputs, retainedAccountId);

        var byId = balances.ToDictionary(b => b.AccountId);
        var lines = computation.OffsetLines
            .Select(l =>
            {
                var b = byId[l.AccountId];
                return new ClosingPreviewLineDto(
                    l.AccountId, b.AccountCode, b.AccountName, b.RootType.ToString(),
                    l.Debit, l.Credit, b.Balance);
            })
            .ToList();

        ClosingPreviewLineDto? retained = computation.RetainedLine is null
            ? null
            : new ClosingPreviewLineDto(
                retainedAccountId, retainedCode, retainedName, nameof(AccountRootType.Equity),
                computation.RetainedLine.Debit, computation.RetainedLine.Credit, -computation.Net);

        return new ClosingPreviewDto(companyId, fiscalYearId, lines, retained, computation.Net);
    }
}
