using Erp.Domain.Entities;
using Erp.Domain.Exceptions;
using Xunit;

namespace Erp.Domain.UnitTests;

/// <summary>
/// tasks.md 2.3 - the <see cref="JournalEntry"/> aggregate and its pure rules, exercised WITHOUT
/// a database (Constitution I.2): the two-step workflow state machine, plan.md §3's canonical
/// balance threshold on the DRAFT lines, the line-structure rules of the create step and the
/// postable-account rules of spec AC-03.
/// </summary>
public sealed class JournalEntryTests
{
    private static readonly DateOnly PostingDate = new(2026, 3, 2);

    private static JournalEntry NewEntry(params (decimal Debit, decimal Credit)[] amounts)
    {
        var entry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            VoucherNo = "JV-2026-00001",
            PostingDate = PostingDate,
            Status = JournalEntryStatus.Draft,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        for (var i = 0; i < amounts.Length; i++)
        {
            entry.Lines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(),
                JournalEntryId = entry.Id,
                LineNumber = i + 1,
                AccountId = Guid.NewGuid(),
                Debit = amounts[i].Debit,
                Credit = amounts[i].Credit,
            });
        }

        return entry;
    }

    // ------------------------------------------------------------------------- lifecycle

    [Fact]
    public void Submit_FromDraft_TransitionsToSubmitted()
    {
        // spec AC-01: "status becomes Submitted".
        var entry = NewEntry((1000m, 0m), (0m, 1000m));

        entry.Submit();

        Assert.Equal(JournalEntryStatus.Submitted, entry.Status);
    }

    [Fact]
    public void Submit_FromSubmitted_ThrowsInvalidStatusTransition()
    {
        var entry = NewEntry((1000m, 0m), (0m, 1000m));
        entry.Submit();

        var ex = Assert.Throws<JournalValidationException>(() => entry.Submit());

        Assert.Equal(JournalErrorCodes.InvalidStatusTransition, ex.Code);
        Assert.Equal(JournalEntryStatus.Submitted, entry.Status); // state untouched
    }

    [Fact]
    public void Submit_FromCancelled_ThrowsInvalidStatusTransition()
    {
        var entry = NewEntry((1000m, 0m), (0m, 1000m));
        entry.Submit();
        entry.Cancel();

        var ex = Assert.Throws<JournalValidationException>(() => entry.Submit());

        Assert.Equal(JournalErrorCodes.InvalidStatusTransition, ex.Code);
        Assert.Equal(JournalEntryStatus.Cancelled, entry.Status);
    }

    [Fact]
    public void Cancel_FromSubmitted_TransitionsToCancelled()
    {
        // spec AC-07: "the original voucher status transitions to Cancelled".
        var entry = NewEntry((1000m, 0m), (0m, 1000m));
        entry.Submit();

        entry.Cancel();

        Assert.Equal(JournalEntryStatus.Cancelled, entry.Status);
    }

    [Fact]
    public void Cancel_FromDraft_ThrowsInvalidStatusTransition()
    {
        // The API maps this to 409 - "cancel this draft" is a workflow violation, not a 400.
        var entry = NewEntry((1000m, 0m), (0m, 1000m));

        var ex = Assert.Throws<JournalValidationException>(() => entry.Cancel());

        Assert.Equal(JournalErrorCodes.InvalidStatusTransition, ex.Code);
        Assert.Equal(JournalEntryStatus.Draft, entry.Status);
    }

    [Fact]
    public void Cancel_FromCancelled_ThrowsInvalidStatusTransition()
    {
        var entry = NewEntry((1000m, 0m), (0m, 1000m));
        entry.Submit();
        entry.Cancel();

        var ex = Assert.Throws<JournalValidationException>(() => entry.Cancel());

        Assert.Equal(JournalErrorCodes.InvalidStatusTransition, ex.Code);
        Assert.Equal(JournalEntryStatus.Cancelled, entry.Status);
    }

    [Fact]
    public void NewEntry_DefaultsToDraft()
    {
        // The two-step workflow starts every voucher as a Draft (ERPNext "save" != "submit").
        var entry = NewEntry((10m, 0m), (0m, 10m));

        Assert.Equal(JournalEntryStatus.Draft, entry.Status);
        Assert.Equal(JournalEntryType.Standard, entry.Type);
    }

    // ------------------------------------------------------------------------- balance (plan §3)

    [Fact]
    public void TotalDebitAndTotalCredit_SumTheLines()
    {
        var entry = NewEntry((1000.5m, 0m), (0m, 500.25m), (0m, 500.25m));

        Assert.Equal(1000.5m, entry.TotalDebit);
        Assert.Equal(1000.5m, entry.TotalCredit);
    }

    [Fact]
    public void EnsureBalanced_EqualTotals_DoesNotThrow()
    {
        // plan.md §3 canonical rule: |totalDebit - totalCredit| <= 0.0001m.
        var entry = NewEntry((995m, 0m), (0m, 995m));

        entry.EnsureBalanced();
    }

    [Fact]
    public void EnsureBalanced_WithinTolerance_DoesNotThrow()
    {
        // The exact threshold of plan §3: a 0.0001 drift is still accepted.
        var entry = NewEntry((1000m, 0m), (0m, 999.9999m));

        entry.EnsureBalanced();
    }

    [Fact]
    public void EnsureBalanced_ToleranceExceeded_ThrowsDoubleEntryImbalance()
    {
        // spec AC-02 Gherkin: debits $1,000.00 vs credits $995.00 -> rejection.
        var entry = NewEntry((1000m, 0m), (0m, 995m));

        var ex = Assert.Throws<DoubleEntryImbalanceException>(() => entry.EnsureBalanced());

        // Carries the stable code the API turns into the 400 ProblemDetails.
        Assert.Equal(StockErrorCodes.DoubleEntryImbalance, ex.Code);
    }

    [Fact]
    public void EnsureBalanced_MissingCreditLine_ThrowsDoubleEntryImbalance()
    {
        var entry = NewEntry((1000m, 0m)); // debit only - no counter-account line at all

        Assert.Throws<DoubleEntryImbalanceException>(() => entry.EnsureBalanced());
    }

    // ------------------------------------------------------------------------- create-step structure rules

    [Fact]
    public void EnsureHasLines_EmptyOrNull_ThrowsNoLines()
    {
        var ex = Assert.Throws<JournalValidationException>(
            () => JournalEntryValidator.EnsureHasLines(null));
        Assert.Equal(JournalErrorCodes.NoLines, ex.Code);

        ex = Assert.Throws<JournalValidationException>(
            () => JournalEntryValidator.EnsureHasLines(Array.Empty<object>()));
        Assert.Equal(JournalErrorCodes.NoLines, ex.Code);
    }

    [Fact]
    public void EnsureHasLines_NonEmpty_DoesNotThrow()
    {
        JournalEntryValidator.EnsureHasLines(new object[] { new object() });
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -0.01)]
    [InlineData(-5, -5)]
    public void EnsureValidLine_NegativeAmount_ThrowsInvalidAmount(decimal debit, decimal credit)
    {
        // Constitution IV.3: Debit >= 0 and Credit >= 0 (the plan §2 CHECK constraints).
        var ex = Assert.Throws<JournalValidationException>(
            () => JournalEntryValidator.EnsureValidLine(debit, credit));

        Assert.Equal(JournalErrorCodes.InvalidAmount, ex.Code);
    }

    [Fact]
    public void EnsureValidLine_BothSidesZero_ThrowsInvalidAmount()
    {
        // A line that posts nothing is a client error, not a silent no-op.
        var ex = Assert.Throws<JournalValidationException>(
            () => JournalEntryValidator.EnsureValidLine(0m, 0m));

        Assert.Equal(JournalErrorCodes.InvalidAmount, ex.Code);
    }

    [Theory]
    [InlineData(100, 0)]
    [InlineData(0, 100)]
    [InlineData(100, 100)] // contra entries (both sides) are legal at the line level
    public void EnsureValidLine_LegalAmounts_DoesNotThrow(decimal debit, decimal credit)
    {
        JournalEntryValidator.EnsureValidLine(debit, credit);
    }

    // ------------------------------------------------------------------------- postable accounts (spec AC-03)

    private static Account NewAccount(
        Guid companyId, bool isGroup = false, bool isActive = true, string code = "1110") =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CompanyId = companyId,
            AccountCode = code,
            AccountName = isGroup ? "Assets" : "Cash at Bank",
            IsGroup = isGroup,
            IsActive = isActive,
        };

    [Fact]
    public void EnsurePostableAccounts_GroupAccount_ThrowsPostingToGroupAccountProhibited()
    {
        // spec AC-03: group = folder, direct postings are strictly forbidden.
        var companyId = Guid.NewGuid();
        var group = NewAccount(companyId, isGroup: true, code: "1000");

        var ex = Assert.Throws<InvalidPostingAccountException>(
            () => JournalEntryValidator.EnsurePostableAccounts(new[] { group }, companyId));

        // The snake_case wire value of spec AC-03's token PostingToGroupAccountProhibited.
        Assert.Equal(AccountingErrorCodes.PostingToGroupAccountProhibited, ex.Code);
        Assert.Equal("1000", ex.AccountCode);
    }

    [Fact]
    public void EnsurePostableAccounts_InactiveAccount_ThrowsInvalidGlAccount()
    {
        var companyId = Guid.NewGuid();
        var inactive = NewAccount(companyId, isActive: false);

        var ex = Assert.Throws<JournalValidationException>(
            () => JournalEntryValidator.EnsurePostableAccounts(new[] { inactive }, companyId));

        Assert.Equal(JournalErrorCodes.InvalidGlAccount, ex.Code);
    }

    [Fact]
    public void EnsurePostableAccounts_ForeignCompany_ThrowsInvalidGlAccount()
    {
        var companyId = Guid.NewGuid();
        var foreign = NewAccount(companyId: Guid.NewGuid()); // another company's COA

        var ex = Assert.Throws<JournalValidationException>(
            () => JournalEntryValidator.EnsurePostableAccounts(new[] { foreign }, companyId));

        Assert.Equal(JournalErrorCodes.InvalidGlAccount, ex.Code);
    }

    [Fact]
    public void EnsurePostableAccounts_LeafActiveSameCompany_DoesNotThrow()
    {
        var companyId = Guid.NewGuid();
        var leaf = NewAccount(companyId);
        var otherLeaf = NewAccount(companyId, code: "5110");

        JournalEntryValidator.EnsurePostableAccounts(new[] { leaf, otherLeaf }, companyId);
    }
}

