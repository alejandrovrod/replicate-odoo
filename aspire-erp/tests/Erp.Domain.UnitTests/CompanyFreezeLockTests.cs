using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// tasks.md 2.2 / spec AC-04 (Gherkin "Freeze Date Lock Enforcement"): the FULL boundary matrix
/// of <see cref="Company.EnsurePostingDateUnlocked"/> - NULL freeze = open, postingDate ==
/// FrozenAccountsDate and postingDate &lt; FrozenAccountsDate = blocked (the literal
/// <c>&lt;=</c> of spec AC-04 / plan.md §3), postingDate &gt; FrozenAccountsDate = allowed.
/// </summary>
public sealed class CompanyFreezeLockTests
{
    private static readonly DateOnly Frozen = new(2025, 12, 31);

    private static Company CompanyWith(DateOnly? frozenAccountsDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Name = "Acme Holding",
            FrozenAccountsDate = frozenAccountsDate,
        };

    [Fact]
    public void EnsurePostingDateUnlocked_NoFreeze_AllowsAnyPostingDate()
    {
        var company = CompanyWith(frozenAccountsDate: null);

        company.EnsurePostingDateUnlocked(new DateOnly(1999, 1, 1)); // must not throw
        company.EnsurePostingDateUnlocked(Frozen);                    // must not throw
        company.EnsurePostingDateUnlocked(new DateOnly(2099, 12, 31)); // must not throw
    }

    [Fact]
    public void EnsurePostingDateUnlocked_PostingDateBeforeFreeze_Throws()
    {
        // AC-04 Gherkin: freeze 2025-12-31, attempt posts on 2025-12-15.
        var company = CompanyWith(Frozen);

        var ex = Assert.Throws<FiscalPeriodLockedException>(
            () => company.EnsurePostingDateUnlocked(new DateOnly(2025, 12, 15)));

        Assert.Equal(new DateOnly(2025, 12, 15), ex.PostingDate);
        Assert.Equal(Frozen, ex.FrozenAccountsDate);
    }

    [Fact]
    public void EnsurePostingDateUnlocked_PostingDateEqualsFreeze_Throws()
    {
        // The literal "<=" of spec AC-04: the frozen day itself is CLOSED.
        var company = CompanyWith(Frozen);

        Assert.Throws<FiscalPeriodLockedException>(
            () => company.EnsurePostingDateUnlocked(Frozen));
    }

    [Fact]
    public void EnsurePostingDateUnlocked_PostingDateAfterFreeze_Allows()
    {
        var company = CompanyWith(Frozen);

        company.EnsurePostingDateUnlocked(new DateOnly(2026, 1, 1)); // must not throw
    }

    [Fact]
    public void FiscalPeriodLockedException_CarriesBothDatesAndStableErrorCode()
    {
        var company = CompanyWith(Frozen);
        var postingDate = new DateOnly(2025, 12, 15);

        var ex = Assert.Throws<FiscalPeriodLockedException>(
            () => company.EnsurePostingDateUnlocked(postingDate));

        // The wire contract: stable snake_case code for the RFC 7807 `code` extension...
        Assert.Equal(AccountingErrorCodes.FiscalPeriodLocked, ex.Code);
        Assert.Equal("fiscal_period_locked", ex.Code);

        // ...and both dates so the API detail can name the boundary.
        Assert.Equal(postingDate, ex.PostingDate);
        Assert.Equal(Frozen, ex.FrozenAccountsDate);
        Assert.Contains("2025-12-15", ex.Message, StringComparison.Ordinal);
        Assert.Contains("2025-12-31", ex.Message, StringComparison.Ordinal);
    }
}

